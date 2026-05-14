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
            // Database context used to check the user's email and password.
            _context = context;
        }

        // Email entered by the user from the login form.
        [BindProperty]
        public string Email { get; set; } = "";

        // Password entered by the user from the login form.
        [BindProperty]
        public string Password { get; set; } = "";

        // Message shown on the login page when login fails.
        public string Message { get; set; } = "";

        public IActionResult OnPost()
        {
            /*
                // IMPORTANT SS THIS:
                Validate required login fields first.

                This prevents the system from checking the database when the user
                did not enter an email or password.
            */
            if (string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password))
            {
                Message = "Please enter your email and password.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Normalize email before searching.

                This makes login email case-insensitive.

                Examples that will now match the same account:
                - test@gmail.com
                - Test@gmail.com
                - TEST@GMAIL.COM

                Trim() removes extra spaces.
                ToLower() makes the email comparison lowercase.
            */
            var normalizedEmail = Email.Trim().ToLower();

            /*
                // IMPORTANT SS THIS:
                Find the account using case-insensitive email comparison.

                u.Email.ToLower() makes the email saved in the database lowercase.
                normalizedEmail is already lowercase.

                This prevents login failure just because the user typed uppercase letters.
            */
            var user = _context.Users
                .FirstOrDefault(u => u.Email.ToLower() == normalizedEmail);

            /*
                // IMPORTANT SS THIS:
                Use a general error message.

                Do not say "Email not found" or "Wrong password" separately.
                This is safer because it does not reveal whether an email exists
                in the system.
            */
            if (user == null)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Password verification using BCrypt.

                The system should NEVER compare plain text passwords directly.

                BCrypt.Verify() compares:
                - the password typed by the user
                - the hashed password saved in the database

                This is important for password security/encryption.
            */
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash);

            if (!isPasswordValid)
            {
                Message = "Invalid email or password.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Clear old session data before creating a new login session.

                This helps prevent old user/admin session values from remaining
                after another account logs in using the same browser.
            */
            HttpContext.Session.Clear();

            /*
                // IMPORTANT SS THIS:
                Determine the role of the logged-in account.

                If user.IsAdmin is true:
                    UserRole = Admin

                If user.IsAdmin is false:
                    UserRole = User

                Your SessionAuthFilter uses this value to protect:
                    /Admin pages -> requires Admin
                    /User pages  -> requires User
            */
            string userRole = user.IsAdmin ? "Admin" : "User";

            /*
                // IMPORTANT SS THIS:
                These session values are created only after successful login.

                UserId:
                    Used to know who is currently logged in.

                UserName:
                    Used to display the logged-in user's name.

                UserEmail:
                    Used when pages need the logged-in user's email.

                UserRole:
                    Used by SessionAuthFilter to check if the account is allowed
                    to access Admin or User pages.

                IsAdmin:
                    Kept for your existing code that may still use it.
            */
            HttpContext.Session.SetInt32("UserId", user.UserId);
            HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
            HttpContext.Session.SetString("UserEmail", user.Email);
            HttpContext.Session.SetString("UserRole", userRole);
            HttpContext.Session.SetString("IsAdmin", user.IsAdmin.ToString());

            /*
                // IMPORTANT SS THIS:
                TempData is used because login redirects to another page.

                TempData can show a success toast/message after redirecting.
            */
            TempData["Success"] = $"Login successful. Welcome, {user.FirstName}!";

            /*
                // IMPORTANT SS THIS:
                Redirect admin users to the admin dashboard.
            */
            if (userRole == "Admin")
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            /*
                // IMPORTANT SS THIS:
                Redirect regular customers to the user home page.
            */
            return RedirectToPage("/User/Home");
        }
    }
}