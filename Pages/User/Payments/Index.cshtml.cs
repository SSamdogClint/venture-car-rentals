using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Payments
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<UserPaymentMethod> PaymentMethods { get; set; } = new();

        [BindProperty]
        public int PaymentMethodId { get; set; }

        [BindProperty]
        public string CardAccountNumber { get; set; } = "";

        [BindProperty]
        public string CardHolderName { get; set; } = "";

        [BindProperty]
        public string ExpiryDate { get; set; } = "";

        [BindProperty]
        public string DetectedCardType { get; set; } = "";

        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadPaymentMethodsAsync(userId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostSaveCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            /*
                The helper validates the demo card number, identifies card type,
                validates expiry date, and returns a masked card number.
                The full 16-digit card number is never saved.
            */
            var validation = PaymentSecurityHelper.ValidateDemoCard(
                CardAccountNumber,
                CardHolderName,
                ExpiryDate
            );

            if (!validation.IsValid)
            {
                ErrorMessage = validation.ErrorMessage;
                await LoadPaymentMethodsAsync(userId.Value);
                return Page();
            }

            /*
                If the same card already exists, reactivate/update it instead of duplicating it.
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

            TempData["Success"] = "Payment method saved successfully.";
            return RedirectToPage("/User/Payments/Index");
        }

        public async Task<IActionResult> OnPostDeactivateCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var card = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserPaymentMethodId == PaymentMethodId &&
                    p.UserId == userId.Value);

            if (card == null)
            {
                TempData["Error"] = "Payment method was not found.";
                return RedirectToPage("/User/Payments/Index");
            }

            /*
                Deactivation does not delete the card.
                It only changes the status so the card becomes unavailable for booking payment.
            */
            card.Status = "inactive";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment method deactivated successfully.";
            return RedirectToPage("/User/Payments/Index");
        }

        public async Task<IActionResult> OnPostActivateCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var card = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserPaymentMethodId == PaymentMethodId &&
                    p.UserId == userId.Value);

            if (card == null)
            {
                TempData["Error"] = "Payment method was not found.";
                return RedirectToPage("/User/Payments/Index");
            }

            card.Status = "active";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment method activated successfully.";
            return RedirectToPage("/User/Payments/Index");
        }

        public async Task<IActionResult> OnPostDeleteCardAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var card = await _context.UserPaymentMethods
                .FirstOrDefaultAsync(p =>
                    p.UserPaymentMethodId == PaymentMethodId &&
                    p.UserId == userId.Value);

            if (card == null)
            {
                TempData["Error"] = "Payment method was not found.";
                return RedirectToPage("/User/Payments/Index");
            }

            /*
                Delete permanently removes the saved payment method from the database.
            */
            _context.UserPaymentMethods.Remove(card);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment method deleted successfully.";
            return RedirectToPage("/User/Payments/Index");
        }

        private async Task LoadPaymentMethodsAsync(int userId)
        {
            /*
                Load both active and inactive cards.
                Active cards appear first, then inactive cards.
            */
            PaymentMethods = await _context.UserPaymentMethods
                .Where(p =>
                    p.UserId == userId &&
                    p.PaymentType == "card")
                .OrderByDescending(p => p.Status == "active")
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();
        }
    }
}