using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Payments
{
    public class PaymentMethodModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public PaymentMethodModel(AppDbContext context, IWebHostEnvironment environment)
        {
            // Database context used for Cars, Bookings, Payments, Rental Agreements,
            // User Payment Methods, User Documents, Users, and Notifications.
            _context = context;

            // Used by the rental agreement generator to save the generated PDF file
            // inside wwwroot.
            _environment = environment;
        }

        public Car? Car { get; set; }

        public List<SavedCardViewModel> SavedCards { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int CarId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowTime { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTime { get; set; }

        [BindProperty]
        public string SelectedPaymentType { get; set; } = "Cash";

        [BindProperty]
        public int? SelectedSavedCardId { get; set; }

        [BindProperty]
        public string CardAccountNumber { get; set; } = "";

        [BindProperty]
        public string CardHolderName { get; set; } = "";

        [BindProperty]
        public string ExpiryDate { get; set; } = "";

        [BindProperty]
        public string DetectedCardType { get; set; } = "";

        [BindProperty]
        public bool AgreementAccepted { get; set; }

        public DateTime BorrowDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }

        public double TotalDays { get; set; }
        public double TotalPrice { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            // IMPORTANT:
            // The payment method page is part of the booking process.
            // Only logged-in users should access this page.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // IMPORTANT:
            // Load selected car, selected booking schedule, calculated total price,
            // and saved card list before displaying the page.
            if (!await LoadPageDataAsync(userId.Value))
            {
                TempData["Error"] = "Payment details are missing or invalid. Please select your booking schedule again.";
                return RedirectToPage("/User/Home");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            // User must be logged in before saving a demo payment method.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Reload the page data so validation errors can return to this same page.
            if (!await LoadPageDataAsync(userId.Value))
            {
                TempData["Error"] = "Payment details are missing or invalid. Please try again.";
                return RedirectToPage("/User/Home");
            }

            // IMPORTANT:
            // This validates the demo card input.
            // It also detects card type, last 4 digits, masked card number,
            // card holder name, and expiration date.
            var validation = PaymentSecurityHelper.ValidateDemoCard(
                CardAccountNumber,
                CardHolderName,
                ExpiryDate
            );

            if (!validation.IsValid)
            {
                ErrorMessage = validation.ErrorMessage;
                return Page();
            }

            // Check if the same card already exists for this user.
            var existingCard = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId.Value &&
                    p.PaymentType == "card" &&
                    p.CardBrand == validation.CardType &&
                    p.Last4 == validation.Last4);

            if (existingCard == null)
            {
                /*
                    IMPORTANT:
                    Never save the full card number.

                    The system saves only:
                    - card brand
                    - masked card number
                    - last 4 digits
                    - card holder name
                    - expiry date

                    This is safer and cleaner for a demo payment feature.
                */
                var newCard = new UserPaymentMethod
                {
                    UserId = userId.Value,
                    PaymentType = "card",
                    CardBrand = validation.CardType,
                    CardHolderName = validation.CardHolderName,
                    MaskedCardNumber = validation.MaskedCardNumber,
                    Last4 = validation.Last4,
                    ExpiryDate = validation.ExpiryDate,
                    Status = "active",
                    IsDefault = false,
                    CreatedAt = DateTime.Now
                };

                _context.UserPaymentMethods.Add(newCard);
            }
            else
            {
                // If the same card exists, update its latest safe display values
                // and reactivate it.
                existingCard.CardHolderName = validation.CardHolderName;
                existingCard.ExpiryDate = validation.ExpiryDate;
                existingCard.MaskedCardNumber = validation.MaskedCardNumber;
                existingCard.Status = "active";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment method saved successfully.";

            return RedirectToPage("/User/Payments/PaymentMethod", new
            {
                carId = CarId,
                borrowDate = BorrowDate,
                borrowTime = BorrowTime,
                returnDate = ReturnDate,
                returnTime = ReturnTime
            });
        }

        public async Task<IActionResult> OnPostCompleteBookingAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            // Redirect to login if there is no active user session.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // IMPORTANT:
            // Reload car, schedule, saved cards, and total amount.
            // This prevents missing values after form submission.
            if (!await LoadPageDataAsync(userId.Value))
            {
                TempData["Error"] = "Booking details are missing or invalid. Please try again.";
                return RedirectToPage("/User/Home");
            }

            // Stop if the selected car no longer exists.
            if (Car == null)
            {
                TempData["Error"] = "Selected car was not found.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            // IMPORTANT:
            // User must accept the online rental agreement before creating
            // the booking request.
            if (!AgreementAccepted)
            {
                ErrorMessage = "You must read and agree to the rental agreement before submitting your booking request.";
                return Page();
            }

            var renter = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            // Stop if the logged-in user cannot be found in the database.
            if (renter == null)
            {
                ErrorMessage = "User account was not found. Please login again.";
                return Page();
            }

            var driverLicenseNumber = await GetDriverLicenseNumberAsync(userId.Value);

            UserPaymentMethod? selectedCard = null;

            // IMPORTANT:
            // If the user selected a saved card, validate that the card:
            // - belongs to the logged-in user
            // - is a card payment method
            // - is active
            if (SelectedSavedCardId != null)
            {
                selectedCard = await _context.UserPaymentMethods
                    .FirstOrDefaultAsync(p =>
                        p.UserPaymentMethodId == SelectedSavedCardId.Value &&
                        p.UserId == userId.Value &&
                        p.PaymentType == "card" &&
                        p.Status == "active");

                if (selectedCard == null)
                {
                    ErrorMessage = "Selected card was not found or is inactive.";
                    return Page();
                }

                SelectedPaymentType = "SavedCard";
            }

            // IMPORTANT:
            // Final availability check before saving booking.
            // This prevents double-booking if another user/admin booked the car
            // after this user opened the page.
            var hasOverlap = await _context.Bookings.AnyAsync(b =>
                b.CarId == CarId &&
                b.Status != "cancelled" &&
                BorrowDateTime < b.EndDate &&
                ReturnDateTime > b.StartDate
            );

            if (hasOverlap)
            {
                TempData["Error"] = "This car is already booked for the selected date and time.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            // IMPORTANT:
            // This transaction saves all related records together:
            // - Booking
            // - Payment
            // - Rental Agreement
            // - Admin Notification
            //
            // If one part fails, everything is rolled back.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                /*
                    IMPORTANT:
                    Booking starts as pending.

                    The user does not directly approve the booking.
                    Admin must review and approve it first.
                */
                var booking = new Booking
                {
                    UserId = userId.Value,
                    CarId = CarId,
                    StartDate = BorrowDateTime,
                    EndDate = ReturnDateTime,
                    TotalPrice = TotalPrice,
                    Status = "pending",
                    CreatedAt = DateTime.Now
                };

                _context.Bookings.Add(booking);

                /*
                    IMPORTANT:
                    Save booking first so EF/database generates BookingId.

                    BookingId is needed by:
                    - Payment
                    - RentalAgreement
                    - Notification message
                */
                await _context.SaveChangesAsync();

                /*
                    IMPORTANT:
                    Pickup-only payment flow.

                    Even if the user chooses a saved demo card, the payment is not
                    completed during customer booking submission.

                    Payment remains pending until admin processes the booking flow.
                */
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = TotalPrice,

                    // Records preferred pickup payment method.
                    PaymentMethod = selectedCard == null
                        ? "cash_pickup"
                        : $"card_pickup_{selectedCard.CardBrand}",

                    // Payment is pending while the booking is still waiting for admin approval.
                    PaymentStatus = "pending_admin_approval",

                    // PaidAt is null because payment is not confirmed yet.
                    PaidAt = null
                };

                _context.Payments.Add(payment);

                var agreementText =
                    "The renter confirms that all provided information is true and correct. " +
                    "The renter agrees to return the vehicle on the selected return date and time. " +
                    "The renter accepts responsibility for damages, late returns, penalties, and other rental charges. " +
                    "The renter understands that this booking is still subject to admin approval. " +
                    "The final signed agreement will be completed face-to-face and uploaded by the admin. " +
                    "Payment is pickup-only and will not be marked as completed until admin approval, agreement signing, and pickup payment completion.";

                // IMPORTANT:
                // Generate a blank rental agreement PDF with empty signature lines.
                // This can later be signed and uploaded/approved by the admin.
                var generatedAgreementFileUrl = RentalAgreementContractGenerator.GenerateBlankAgreementFile(
                    _environment.WebRootPath,
                    booking,
                    renter,
                    Car,
                    agreementText,
                    driverLicenseNumber
                );

                // Save rental agreement record connected to the booking.
                var rentalAgreement = new RentalAgreement
                {
                    BookingId = booking.BookingId,
                    AgreementText = agreementText,
                    Status = "online_accepted",
                    OnlineAcceptedAt = DateTime.Now,
                    GeneratedAgreementFileUrl = generatedAgreementFileUrl,
                    GeneratedAt = DateTime.Now
                };

                _context.RentalAgreements.Add(rentalAgreement);

                /*
                    IMPORTANT:
                    Notify admin that a new booking is waiting for review.
                */
                _context.Notifications.Add(new Notification
                {
                    RecipientType = "admin",
                    UserId = null,
                    Title = "New Pending Booking",
                    Message = $"{renter.FirstName} {renter.LastName} submitted booking #{booking.BookingId}.",
                    Type = "booking",
                    TargetUrl = "/Admin/Bookings/BookingList?tab=pending",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                /*
                    IMPORTANT:
                    This saves:
                    - Payment
                    - RentalAgreement
                    - Notification
                */
                await _context.SaveChangesAsync();

                // Commit the transaction only after all records are saved.
                await transaction.CommitAsync();

                /*
                    IMPORTANT:
                    These TempData values are used by the modal in My Bookings.

                    BookingRequestSubmitted:
                    - tells My Bookings page to show the success modal

                    BookingPickupDate:
                    - displays the pickup schedule inside the modal
                */
                TempData["BookingRequestSubmitted"] = "true";
                TempData["BookingPickupDate"] = BorrowDateTime.ToString("MMMM dd, yyyy hh:mm tt");

                // Redirect to My Bookings where the modal will appear.
                return RedirectToPage("/User/Bookings/Index");
            }
            catch
            {
                // IMPORTANT:
                // Rollback prevents incomplete records if booking, payment,
                // agreement generation, or notification creation fails.
                await transaction.RollbackAsync();

                ErrorMessage = "Something went wrong while submitting your booking request. Please try again.";
                return Page();
            }
        }

        private async Task<string> GetDriverLicenseNumberAsync(int userId)
        {
            // For local renters, use driver's license.
            // For foreign renters, use international driving permit if available.
            var document = await _context.UserDocuments
                .Where(d =>
                    d.UserId == userId &&
                    (d.DocType == "driver_license" || d.DocType == "international_driving_permit"))
                .OrderByDescending(d => d.DocType == "driver_license")
                .FirstOrDefaultAsync();

            return document?.DocNumber ?? "";
        }

        private async Task<bool> LoadPageDataAsync(int userId)
        {
            if (!LoadSchedule())
            {
                return false;
            }

            Car = await _context.Cars.FindAsync(CarId);

            if (Car == null || Car.Status != "available")
            {
                return false;
            }

            // IMPORTANT:
            // Price calculation is based on number of rental days.
            // Math.Ceiling is used so partial days still count as 1 rental day.
            TotalPrice = TotalDays * (double)Car.PricePerDay;

            // Load active and inactive cards.
            // Inactive cards can be visible in the UI, but should not be selectable
            // for completing a booking.
            SavedCards = await _context.UserPaymentMethods
                .Where(p =>
                    p.UserId == userId &&
                    p.PaymentType == "card")
                .OrderByDescending(p => p.Status == "active")
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new SavedCardViewModel
                {
                    PaymentMethodId = p.UserPaymentMethodId,
                    CardType = p.CardBrand,
                    CardHolderName = p.CardHolderName,
                    Last4 = p.Last4,
                    ExpiryDate = p.ExpiryDate,
                    MaskedCardNumber = p.MaskedCardNumber,
                    Status = p.Status
                })
                .ToListAsync();

            return true;
        }

        private bool LoadSchedule()
        {
            if (string.IsNullOrWhiteSpace(BorrowDate) ||
                string.IsNullOrWhiteSpace(BorrowTime) ||
                string.IsNullOrWhiteSpace(ReturnDate) ||
                string.IsNullOrWhiteSpace(ReturnTime))
            {
                return false;
            }

            if (!DateTime.TryParse($"{BorrowDate} {BorrowTime}", out DateTime borrowDateTime) ||
                !DateTime.TryParse($"{ReturnDate} {ReturnTime}", out DateTime returnDateTime))
            {
                return false;
            }

            if (borrowDateTime >= returnDateTime)
            {
                return false;
            }

            BorrowDateTime = borrowDateTime;
            ReturnDateTime = returnDateTime;

            // IMPORTANT:
            // Any partial day counts as a full rental day.
            // Example: 25 hours = 2 days.
            TotalDays = Math.Ceiling((ReturnDateTime - BorrowDateTime).TotalHours / 24);

            return true;
        }
    }

    public class SavedCardViewModel
    {
        public int PaymentMethodId { get; set; }

        public string CardType { get; set; } = "";

        public string CardHolderName { get; set; } = "";

        public string Last4 { get; set; } = "";

        public string ExpiryDate { get; set; } = "";

        public string MaskedCardNumber { get; set; } = "";

        public string Status { get; set; } = "";
    }
}