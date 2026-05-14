using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        public RegisterModel(AppDbContext context)
        {
            // Database context used to save and check user accounts.
            _context = context;
        }

        // User first name from the registration form.
        [BindProperty]
        public string FirstName { get; set; } = "";

        // User last name from the registration form.
        [BindProperty]
        public string LastName { get; set; } = "";

        // User email from the registration form.
        [BindProperty]
        public string Email { get; set; } = "";

        // User birthday from the registration form.
        [BindProperty]
        public DateTime? Birthday { get; set; }

        // User password from the registration form.
        [BindProperty]
        public string Password { get; set; } = "";

        // Password confirmation from the registration form.
        [BindProperty]
        public string ConfirmPassword { get; set; } = "";

        // Success message shown after successful registration.
        public string Message { get; set; } = "";

        // Error message shown when validation fails.
        public string ErrorMessage { get; set; } = "";

        public IActionResult OnPost()
        {
            /*
                // IMPORTANT SS THIS:
                Clean user inputs before validation and saving.

                Trim() removes extra spaces from the beginning and end.

                Example:
                " Clint " becomes "Clint"
                " user@gmail.com " becomes "user@gmail.com"
            */
            FirstName = FirstName.Trim();
            LastName = LastName.Trim();
            Email = Email.Trim();

            /*
                // IMPORTANT SS THIS:
                Validate required fields first.

                This prevents the system from saving incomplete accounts.
            */
            if (string.IsNullOrWhiteSpace(FirstName) ||
                string.IsNullOrWhiteSpace(LastName) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please complete all required fields.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Normalize email before duplicate checking and saving.

                This makes email handling case-insensitive.

                Examples treated as the same email:
                - test@gmail.com
                - Test@gmail.com
                - TEST@GMAIL.COM

                The account will be saved as lowercase for consistency.
            */
            var normalizedEmail = Email.ToLower();

            /*
                // IMPORTANT SS THIS:
                Birthday is required because the system must check if the renter is 18+.

                This helps prevent minors from creating a car rental account.
            */
            if (Birthday == null)
            {
                ErrorMessage = "Please enter your birthday.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Minor registration restriction.

                Users below 18 years old cannot register because car rental requires
                valid renter eligibility, verification, and agreement signing.
            */
            if (!AgeValidationHelper.IsAtLeast18(Birthday))
            {
                ErrorMessage = AgeValidationHelper.UnderAgeMessage;
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Validate password confirmation before saving the account.

                This prevents the user from creating an account with a mistyped password.
            */
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            /*
                // IMPORTANT SS THIS:
                Prevent duplicate email registration using case-insensitive checking.

                Without this, the system might allow duplicate emails like:
                - user@gmail.com
                - User@gmail.com
                - USER@gmail.com
            */
            var existingUser = _context.Users
                .FirstOrDefault(u => u.Email.ToLower() == normalizedEmail);

            if (existingUser != null)
            {
                ErrorMessage = "Email already exists.";
                return Page();
            }

            try
            {
                /*
                    // IMPORTANT SS THIS:
                    Create a new customer account.

                    CreatedAt must be set to DateTime.Now.
                    If CreatedAt is not assigned, it can show as January 1, 0001
                    in the admin verification page.
                */
                var user = new VentureCarRentals.Models.User
                {
                    FirstName = FirstName,
                    LastName = LastName,

                    /*
                        // IMPORTANT SS THIS:
                        Save email as lowercase.

                        This keeps all emails consistent in the database and avoids
                        login/duplicate issues caused by uppercase/lowercase letters.
                    */
                    Email = normalizedEmail,

                    Birthday = Birthday,

                    /*
                        // IMPORTANT SS THIS:
                        Password security.

                        Never save plain text passwords in the database.

                        BCrypt.HashPassword() converts the password into a secure hash.
                        During login, BCrypt.Verify() checks the typed password against
                        this saved hash.
                    */
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),

                    // New registered users are normal customers, not admins.
                    IsAdmin = false,

                    // Date and time when the account was created.
                    CreatedAt = DateTime.Now
                };

                // Add the new user to the database.
                _context.Users.Add(user);

                /*
                    // IMPORTANT SS THIS:
                    Save the new user account permanently in the database.
                */
                _context.SaveChanges();

                // Success message shown on the Register page.
                Message = "Registration successful! Redirecting to login...";
                return Page();
            }
            catch
            {
                /*
                    // IMPORTANT SS THIS:
                    General error handling.

                    This prevents the app from crashing if something unexpected happens
                    while saving the account.
                */
                ErrorMessage = "Something went wrong while creating your account. Please try again.";
                return Page();
            }
        }
    }
}