using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
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

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            PaymentMethods = await _context.UserPaymentMethods
                .Where(p => p.UserId == userId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Page();
        }
    }
}