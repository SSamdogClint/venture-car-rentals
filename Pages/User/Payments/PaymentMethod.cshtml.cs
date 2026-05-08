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
            _context = context;
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

            // User must be logged in before accessing the booking payment page.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Load selected car, schedule, total amount, and saved cards.
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

            // User must be logged in before saving a payment method.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Reload page data so validation errors can return to this same page.
            if (!await LoadPageDataAsync(userId.Value))
            {
                TempData["Error"] = "Payment details are missing or invalid. Please try again.";
                return RedirectToPage("/User/Home");
            }

            // Validate demo card details using your security helper.
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

            // Check if the same saved card already exists for this user.
            var existingCard = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId.Value &&
                    p.PaymentType == "card" &&
                    p.CardBrand == validation.CardType &&
                    p.Last4 == validation.Last4);

            if (existingCard == null)
            {
                // IMPORTANT:
                // Never save the full 16-digit card number.
                // Only save masked card number and last 4 digits.
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
                // If the same card exists, update and reactivate it.
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

            // Reload car, schedule, total amount, and saved cards.
            if (!await LoadPageDataAsync(userId.Value))
            {
                TempData["Error"] = "Booking details are missing or invalid. Please try again.";
                return RedirectToPage("/User/Home");
            }

            // Stop if selected car is missing.
            if (Car == null)
            {
                TempData["Error"] = "Selected car was not found.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            // User must accept the online rental agreement before creating the booking request.
            if (!AgreementAccepted)
            {
                ErrorMessage = "You must read and agree to the rental agreement before submitting your booking request.";
                return Page();
            }

            var renter = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId.Value);

            // Stop if the user record is missing from the database.
            if (renter == null)
            {
                ErrorMessage = "User account was not found. Please login again.";
                return Page();
            }

            var driverLicenseNumber = await GetDriverLicenseNumberAsync(userId.Value);

            UserPaymentMethod? selectedCard = null;

            // If user selected a saved card, only active cards owned by the user are allowed.
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

            // Final availability check to prevent double-booking.
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

            // Transaction saves Booking, Payment, and RentalAgreement together.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                /*
                    IMPORTANT FEATURE:
                    Booking is created as pending first.

                    It should NOT become approved here.
                    Admin must review the booking, signed agreement,
                    and payment arrangement before approval.
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
                await _context.SaveChangesAsync();

                /*
                    IMPORTANT FEATURE:
                    Pickup-only payment.

                    Even if the user selects a saved demo card, the system does NOT mark it as paid.
                    Payment stays pending until admin approval, agreement signing, and pickup payment completion.
                */
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = TotalPrice,

                    // This records the preferred pickup payment method only.
                    PaymentMethod = selectedCard == null
                        ? "cash_pickup"
                        : $"card_pickup_{selectedCard.CardBrand}",

                    // Payment is not completed while booking is still pending.
                    PaymentStatus = "pending_admin_approval",

                    // PaidAt remains null until admin confirms payment.
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

                // Generate blank PDF rental agreement with empty signature lines.
                var generatedAgreementFileUrl = RentalAgreementContractGenerator.GenerateBlankAgreementFile(
                    _environment.WebRootPath,
                    booking,
                    renter,
                    Car,
                    agreementText,
                    driverLicenseNumber
                );

                // Save rental agreement record with generated blank PDF path.
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                /*
                    SUCCESS MESSAGE:
                    This appears as a toast on My Bookings after redirect.
                */
                TempData["Success"] =
                    $"Booking request submitted successfully. Please wait for admin approval. " +
                    $"Once approved, pick up the car on {BorrowDateTime:MMM dd, yyyy hh:mm tt}. " +
                    $"During pickup, you must sign the rental agreement contract and complete your payment at the rental office. " +
                    $"Your payment will remain pending until the agreement signing and payment confirmation are completed.";

                return RedirectToPage("/User/Bookings/Index", new
                {
                    Tab = "pending"
                });
            }
            catch
            {
                // Rollback prevents incomplete records if booking, payment, or agreement generation fails.
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

            // Price calculation based on number of rental days.
            TotalPrice = TotalDays * (double)Car.PricePerDay;

            // Load both active and inactive cards.
            // Inactive cards are visible but cannot be selected for booking payment.
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