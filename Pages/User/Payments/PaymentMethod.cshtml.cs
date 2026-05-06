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
                Error handling and security validation are handled by PaymentSecurityHelper.
                This keeps the Razor Page model cleaner and easier to explain.
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
                Security:
                Do not save the full 16-digit card number.
                Save only the masked card number and last 4 digits.
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
                    IsDefault = false,
                    CreatedAt = DateTime.Now
                };

                _context.UserPaymentMethods.Add(newCard);
                await _context.SaveChangesAsync();
            }

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

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            if (!await LoadPageDataAsync(userId.Value))
            {
                return RedirectToPage("/User/Home");
            }

            if (Car == null)
            {
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            UserPaymentMethod? selectedCard = null;

            if (SelectedSavedCardId != null)
            {
                selectedCard = await _context.UserPaymentMethods
                    .FirstOrDefaultAsync(p =>
                        p.UserPaymentMethodId == SelectedSavedCardId.Value &&
                        p.UserId == userId.Value &&
                        p.PaymentType == "card");

                if (selectedCard == null)
                {
                    ErrorMessage = "Selected card was not found.";
                    return Page();
                }

                SelectedPaymentType = "SavedCard";
            }

            /*
                Final availability check before saving the booking.
                This prevents double-booking if another user books the same car
                while this user is on the payment page.
            */
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

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                /*
                    Booking is created only after payment method confirmation.
                    This means the booking is not completed before payment selection.
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
                    Payment record is connected to the newly created booking.
                    Cash is unpaid, while saved demo card is marked as paid.
                */
                var payment = new Payment
                {
                    BookingId = booking.BookingId,
                    Amount = TotalPrice,
                    PaymentMethod = selectedCard == null ? "cash" : $"card_{selectedCard.CardBrand}",
                    PaymentStatus = selectedCard == null ? "unpaid" : "paid",
                    PaidAt = selectedCard == null ? null : DateTime.Now
                };

                _context.Payments.Add(payment);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Booking completed successfully. Your booking is now pending admin confirmation.";

                return RedirectToPage("/User/Home");
            }
            catch
            {
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

            SavedCards = await _context.UserPaymentMethods
                .Where(p => p.UserId == userId && p.PaymentType == "card")
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new SavedCardViewModel
                {
                    PaymentMethodId = p.UserPaymentMethodId,
                    CardType = p.CardBrand,
                    CardHolderName = p.CardHolderName,
                    Last4 = p.Last4,
                    ExpiryDate = p.ExpiryDate,
                    MaskedCardNumber = p.MaskedCardNumber
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
    }
}