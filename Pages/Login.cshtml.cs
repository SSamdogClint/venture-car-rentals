using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty] public string Email { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";

        public string Message { get; set; } = "";

        public IActionResult OnPost()
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == Email);

            if (user == null)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.FirstName + " " + user.LastName);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

            if (user.IsAdmin)
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            return RedirectToPage("/User/Home");
        }
    }
}