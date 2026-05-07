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

        public PaymentMethodModel(AppDbContext context)
        {
            _context = context;
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
        public bool AgreementAccepted { get; set; }

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

        public DateTime BorrowDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }

        public double TotalDays { get; set; }
        public double TotalPrice { get; set; }

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            if (!await LoadPageDataAsync(userId.Value))
            {
                return RedirectToPage("/User/Home");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            if (!await LoadPageDataAsync(userId.Value))
            {
                return RedirectToPage("/User/Home");
            }

            /*
                PaymentSecurityHelper handles validation and masking.
                The full 16-digit card number is not stored in the database.
            */
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

            /*
                If the card already exists, update/reactivate it.
                Otherwise, save it as a new active payment method.
            */
            var existingCard = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserId == userId.Value &&
                    p.PaymentType == "card" &&
                    p.CardBrand == validation.CardType &&
                    p.Last4 == validation.Last4);

            if (existingCard == null)
            {
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
                existingCard.CardHolderName = validation.CardHolderName;
                existingCard.ExpiryDate = validation.ExpiryDate;
                existingCard.MaskedCardNumber = validation.MaskedCardNumber;
                existingCard.Status = "active";
            }

            await _context.SaveChangesAsync();

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

            // Redirect user to login if there is no active session.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            // Reload car, schedule, total price, and saved payment methods.
            // If the schedule data is invalid or missing, send the user back home.
            if (!await LoadPageDataAsync(userId.Value))
            {
                return RedirectToPage("/User/Home");
            }

            // Make sure the selected car still exists.
            if (Car == null)
            {
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            // The booking cannot continue unless the user accepts the online rental agreement.
            if (!AgreementAccepted)
            {
                ErrorMessage = "You must read and agree to the rental agreement before completing the booking.";
                return Page();
            }

            UserPaymentMethod? selectedCard = null;

            // If the user selected a saved card, the system checks if the card exists,
            // belongs to the logged-in user, and is still active.
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

            // Final availability check before saving.
            // This prevents two users from booking the same car on overlapping dates.
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

            // A transaction is used so Booking, Payment, and Rental Agreement are saved together.
            // If one save fails, all changes are rolled back.
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Create the booking only after payment method and agreement confirmation.
                // Status is pending because admin still needs to review it.
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

                // Create the payment record connected to the booking.
                // Cash is marked unpaid, while saved demo card is marked paid.
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = TotalPrice,
                    PaymentMethod = selectedCard == null ? "cash" : $"card_{selectedCard.CardBrand}",
                    PaymentStatus = selectedCard == null ? "unpaid" : "paid",
                    PaidAt = selectedCard == null ? null : DateTime.Now
                };

                _context.Payments.Add(payment);

                // This text is saved as the user's online agreement confirmation.
                // The admin can later upload the signed face-to-face agreement.
                var agreementText =
                    "The renter confirms that all provided information is true and correct. " +
                    "The renter agrees to return the vehicle on the selected return date and time. " +
                    "The renter accepts responsibility for damages, late returns, penalties, and other rental charges. " +
                    "The renter understands that this booking is still subject to admin approval.";

                // Create the rental agreement record after the user checks the agreement checkbox.
                var rentalAgreement = new RentalAgreement
                {
                    BookingId = booking.BookingId,
                    AgreementText = agreementText,
                    Status = "online_accepted",
                    OnlineAcceptedAt = DateTime.Now
                };

                _context.RentalAgreements.Add(rentalAgreement);

                // Save payment and agreement, then commit the whole transaction.
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Booking completed successfully. Your online rental agreement has been accepted and your booking is now pending admin approval.";

                return RedirectToPage("/User/Home");
            }
            catch
            {
                // If there is an error, undo all database changes from this transaction.
                await transaction.RollbackAsync();

                ErrorMessage = "Something went wrong while completing your booking. Please try again.";
                return Page();
            }
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

            TotalPrice = TotalDays * Car.PricePerDay;

            /*
                Load both active and inactive cards.
                The UI will show inactive cards as gray and disabled.
            */
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