using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Cars
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public Car? Car { get; set; }
        public Models.User? Renter { get; set; }

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

        public DateTime BorrowDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }

        public double TotalDays { get; set; }
        public double TotalPrice { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            Renter = user;

            if (!LoadSchedule())
            {
                return RedirectToPage("/User/Home");
            }

            var verificationStatus = await GetVerificationStatusAsync(user.UserId, user);

            if (verificationStatus == "needs_requirements")
            {
                return RedirectToProfile();
            }

            if (verificationStatus == "underage")
            {
                TempData["Error"] = AgeValidationHelper.UnderAgeMessage;
                return RedirectToProfile();
            }

            if (verificationStatus == "pending")
            {
                TempData["VerificationSubmitted"] =
                    "Your verification is still pending. Please wait for 30 minutes to 1 day while the admin reviews your submitted requirements.";

                return RedirectToPage("/User/Home");
            }

            if (verificationStatus == "rejected")
            {
                TempData["Error"] = "One or more of your verification documents were rejected. Please update your documents.";
                return RedirectToProfile();
            }

            if (verificationStatus == "expired")
            {
                TempData["Error"] = "One or more of your verification documents are expired. Please renew your documents.";
                return RedirectToProfile();
            }

            if (verificationStatus != "verified")
            {
                return RedirectToPage("/Login");
            }

            Car = await _context.Cars.FindAsync(CarId);

            if (Car == null)
            {
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            if (Car.Status != "available")
            {
                TempData["Error"] = "This car is not available.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

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

            TotalPrice = TotalDays * Car.PricePerDay;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            if (!LoadSchedule())
            {
                return RedirectToPage("/User/Home");
            }

            var verificationStatus = await GetVerificationStatusAsync(user.UserId, user);

            if (verificationStatus == "needs_requirements")
            {
                /*
                    IMPORTANT FIX:
                    Old route was /User/Documents/CompleteRequirements.
                    Verification is now inside /User/Profile/Index.
                */
                return RedirectToProfile();
            }

            if (verificationStatus == "underage")
            {
                TempData["Error"] = AgeValidationHelper.UnderAgeMessage;
                return RedirectToProfile();
            }

            if (verificationStatus == "pending")
            {
                TempData["VerificationSubmitted"] =
                    "Your verification is still pending. Please wait for 30 minutes to 1 day while the admin reviews your submitted requirements.";

                return RedirectToPage("/User/Home");
            }

            if (verificationStatus == "rejected")
            {
                TempData["Error"] = "One or more of your verification documents were rejected. Please update your documents.";
                return RedirectToProfile();
            }

            if (verificationStatus == "expired")
            {
                TempData["Error"] = "One or more of your verification documents are expired. Please renew your documents.";
                return RedirectToProfile();
            }

            if (verificationStatus != "verified")
            {
                return RedirectToPage("/Login");
            }

            var car = await _context.Cars.FindAsync(CarId);

            if (car == null || car.Status != "available")
            {
                TempData["Error"] = "This car is not available.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

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

            return RedirectToPage("/User/Payments/PaymentMethod", new
            {
                carId = CarId,
                borrowDate = BorrowDate,
                borrowTime = BorrowTime,
                returnDate = ReturnDate,
                returnTime = ReturnTime
            });
        }

        private IActionResult RedirectToProfile()
        {
            return RedirectToPage("/User/Profile/Index", new
            {
                carId = CarId,
                borrowDate = BorrowDate,
                borrowTime = BorrowTime,
                returnDate = ReturnDate,
                returnTime = ReturnTime
            });
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

        private async Task<string> GetVerificationStatusAsync(int userId, Models.User user)
        {
            if (user.Birthday == null)
            {
                return "needs_requirements";
            }

            /*
                IMPORTANT:
                User must be at least 18 years old before booking.
            */
            if (!AgeValidationHelper.IsAtLeast18(user.Birthday))
            {
                return "underage";
            }

            if (!IsProfileComplete(user))
            {
                return "needs_requirements";
            }

            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .ToListAsync();

            var requiredInfo = GetRequiredDocumentInfo(user, documents);

            if (!requiredInfo.HasAnySubmitted)
            {
                return "needs_requirements";
            }

            if (requiredInfo.HasExpired)
            {
                return "expired";
            }

            if (requiredInfo.HasRejected)
            {
                return "rejected";
            }

            if (requiredInfo.SubmittedCount == requiredInfo.RequiredTotal &&
                requiredInfo.ApprovedCount == requiredInfo.RequiredTotal)
            {
                return "verified";
            }

            return "pending";
        }

        private CreateRequiredDocumentInfo GetRequiredDocumentInfo(Models.User user, List<UserDocument> documents)
        {
            var requiredDocuments = new List<UserDocument>();

            var userCountry = user.Country?.ToLower() ?? "";
            var isForeign = userCountry != "philippines";

            if (isForeign)
            {
                var passport = documents
                    .Where(d => d.DocType == "passport")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefault();

                var permit = documents
                    .Where(d => d.DocType == "international_driving_permit")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefault();

                if (passport != null)
                {
                    requiredDocuments.Add(passport);
                }

                if (permit != null)
                {
                    requiredDocuments.Add(permit);
                }

                return new CreateRequiredDocumentInfo
                {
                    RequiredTotal = 2,
                    SubmittedCount = requiredDocuments.Count,
                    ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                    HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                    HasExpired = requiredDocuments.Any(IsExpired),
                    HasAnySubmitted = requiredDocuments.Any()
                };
            }

            var secondaryDocTypes = new[]
            {
                "national_id",
                "police_clearance",
                "nbi_clearance",
                "philhealth_id",
                "sss_id",
                "umid",
                "voters_id",
                "company_id"
            };

            var driverLicense = documents
                .Where(d => d.DocType == "driver_license")
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefault();

            var secondaryId = documents
                .Where(d => secondaryDocTypes.Contains(d.DocType))
                .OrderByDescending(d => d.Status == "approved")
                .ThenByDescending(d => d.UploadedAt)
                .FirstOrDefault();

            if (driverLicense != null)
            {
                requiredDocuments.Add(driverLicense);
            }

            if (secondaryId != null)
            {
                requiredDocuments.Add(secondaryId);
            }

            return new CreateRequiredDocumentInfo
            {
                RequiredTotal = 2,
                SubmittedCount = requiredDocuments.Count,
                ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                HasExpired = requiredDocuments.Any(IsExpired),
                HasAnySubmitted = requiredDocuments.Any()
            };
        }

        private bool IsProfileComplete(Models.User user)
        {
            var userCountry = user.Country?.ToLower() ?? "";
            var isForeign = userCountry != "philippines";

            /*
                IMPORTANT FIX:
                Foreign renters do not need Street, Barangay, and State.
            */
            if (isForeign)
            {
                return !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                       !string.IsNullOrWhiteSpace(user.City) &&
                       !string.IsNullOrWhiteSpace(user.ZipCode) &&
                       !string.IsNullOrWhiteSpace(user.Country) &&
                       user.Birthday != null;
            }

            return !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(user.Street) &&
                   !string.IsNullOrWhiteSpace(user.Barangay) &&
                   !string.IsNullOrWhiteSpace(user.City) &&
                   !string.IsNullOrWhiteSpace(user.State) &&
                   !string.IsNullOrWhiteSpace(user.ZipCode) &&
                   !string.IsNullOrWhiteSpace(user.Country) &&
                   user.Birthday != null;
        }

        private bool IsExpired(UserDocument document)
        {
            return document.ExpiryDate != null &&
                   document.ExpiryDate.Value.Date < DateTime.Today;
        }
    }

    /*
        IMPORTANT:
        This class name is CreateRequiredDocumentInfo to avoid conflict
        with BrowseRequiredDocumentInfo in BrowseCars.cshtml.cs.
    */
    public class CreateRequiredDocumentInfo
    {
        public int RequiredTotal { get; set; }
        public int SubmittedCount { get; set; }
        public int ApprovedCount { get; set; }
        public bool HasRejected { get; set; }
        public bool HasExpired { get; set; }
        public bool HasAnySubmitted { get; set; }
    }
}