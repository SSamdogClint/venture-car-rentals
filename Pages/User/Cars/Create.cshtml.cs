using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
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
                return RedirectToPage("/User/Documents/CompleteRequirements", new
                {
                    carId = CarId,
                    borrowDate = BorrowDate,
                    borrowTime = BorrowTime,
                    returnDate = ReturnDate,
                    returnTime = ReturnTime
                });
            }

            if (verificationStatus == "pending")
            {
                TempData["VerificationSubmitted"] = "Your verification is still pending. Please wait for 30 minutes to 1 day while the admin reviews your submitted requirements.";
                return RedirectToPage("/User/Home");
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
                return RedirectToPage("/User/Documents/CompleteRequirements", new
                {
                    carId = CarId,
                    borrowDate = BorrowDate,
                    borrowTime = BorrowTime,
                    returnDate = ReturnDate,
                    returnTime = ReturnTime
                });
            }

            if (verificationStatus == "pending")
            {
                TempData["VerificationSubmitted"] = "Your verification is still pending. Please wait for 30 minutes to 1 day while the admin reviews your submitted requirements.";
                return RedirectToPage("/User/Home");
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
            var hasProfile =
                !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                !string.IsNullOrWhiteSpace(user.Street) &&
                !string.IsNullOrWhiteSpace(user.Barangay) &&
                !string.IsNullOrWhiteSpace(user.City) &&
                !string.IsNullOrWhiteSpace(user.State) &&
                !string.IsNullOrWhiteSpace(user.ZipCode) &&
                !string.IsNullOrWhiteSpace(user.Country) &&
                user.Birthday != null;

            if (!hasProfile)
            {
                return "needs_requirements";
            }

            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .ToListAsync();

            var isForeign = user.Country.ToLower() != "philippines";

            if (isForeign)
            {
                var hasPassport = documents.Any(d => d.DocType == "passport");
                var hasInternationalPermit = documents.Any(d => d.DocType == "international_driving_permit");

                if (!hasPassport || !hasInternationalPermit)
                {
                    return "needs_requirements";
                }

                var approvedPassport = documents.Any(d =>
                    d.DocType == "passport" &&
                    d.Status == "approved");

                var approvedInternationalPermit = documents.Any(d =>
                    d.DocType == "international_driving_permit" &&
                    d.Status == "approved");

                return approvedPassport && approvedInternationalPermit ? "verified" : "pending";
            }
            else
            {
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

                var hasDriverLicense = documents.Any(d => d.DocType == "driver_license");
                var hasSecondaryId = documents.Any(d => secondaryDocTypes.Contains(d.DocType));

                if (!hasDriverLicense || !hasSecondaryId)
                {
                    return "needs_requirements";
                }

                var approvedDriverLicense = documents.Any(d =>
                    d.DocType == "driver_license" &&
                    d.Status == "approved");

                var approvedSecondaryId = documents.Any(d =>
                    secondaryDocTypes.Contains(d.DocType) &&
                    d.Status == "approved");

                return approvedDriverLicense && approvedSecondaryId ? "verified" : "pending";
            }
        }
    }
}