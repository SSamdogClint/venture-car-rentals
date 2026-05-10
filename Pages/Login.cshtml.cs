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

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string Password { get; set; } = "";

        public string Message { get; set; } = "";

        public IActionResult OnPost()
        {
            // Find the account using the email entered by the user.
            var user = _context.Users.FirstOrDefault(u => u.Email == Email);

            if (user == null)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            // Verify the entered password against the hashed password stored in the database.
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            /*
                IMPORTANT:
                This determines the role of the logged-in account.

                If user.IsAdmin is true:
                    UserRole = Admin

                If user.IsAdmin is false:
                    UserRole = User

                The reusable SessionAuthFilter will use this value to protect:
                    /Admin pages -> requires Admin
                    /User pages  -> requires User
            */
            string userRole = user.IsAdmin ? "Admin" : "User";

            /*
                IMPORTANT:
                These session values are created after successful login.

                UserId:
                    Used to know who is currently logged in.

                UserName:
                    Used to display the user's name in the UI.

                UserEmail:
                    Used if pages need the logged-in user's email.

                UserRole:
                    Used by SessionAuthFilter to check if the user is allowed
                    to access Admin or User pages.

                IsAdmin:
                    Kept for your existing code that may still use it.
            */
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", user.FirstName + " " + user.LastName);
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", userRole);
            HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

            /*
                IMPORTANT FEATURE:
                TempData is used because login redirects to another page.
                The success message can appear as a toast after redirecting.
            */
            TempData["Success"] = $"Login successful. Welcome, {user.FirstName}!";

            // Redirect admin users to the admin dashboard.
            if (userRole == "Admin")
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            // Redirect regular users to the user home page.
            return RedirectToPage("/User/Home");
        }
    }
}