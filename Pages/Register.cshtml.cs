using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty] public string FirstName { get; set; } = "";
        [BindProperty] public string LastName { get; set; } = "";
        [BindProperty] public string Email { get; set; } = "";
        [BindProperty] public string Password { get; set; } = "";
        [BindProperty] public string ConfirmPassword { get; set; } = "";

        public string Message { get; set; } = "";
        public string ErrorMessage { get; set; } = "";

        public IActionResult OnPost()
        {
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            var existingUser = _context.Users.FirstOrDefault(u => u.Email == Email);

            if (existingUser != null)
            {
                ErrorMessage = "Email already exists.";
                return Page();
            }

            var user = new VentureCarRentals.Models.User
            {
                FirstName = FirstName,
                LastName = LastName,
                Email = Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                IsAdmin = false
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            Message = "Registration successful! Redirecting to login...";
            return Page();
        }
    }
}