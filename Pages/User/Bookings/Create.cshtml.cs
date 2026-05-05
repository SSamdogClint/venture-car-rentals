using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Bookings
{
    public class CreateModel : PageModel
    {
        private readonly AppDbContext _context;

        public CreateModel(AppDbContext context)
        {
            _context = context;
        }

        public Car? Car { get; set; }

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

            var isComplete = await IsUserReadyToBookAsync(user.UserId, user);

            if (!isComplete)
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

            var isValidSchedule = LoadSchedule();

            if (!isValidSchedule)
            {
                return RedirectToPage("/User/Home");
            }

            Car = await _context.Cars.FindAsync(CarId);

            if (Car == null)
            {
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

            var isValidSchedule = LoadSchedule();

            if (!isValidSchedule)
            {
                return RedirectToPage("/User/Home");
            }

            var car = await _context.Cars.FindAsync(CarId);

            if (car == null || car.Status != "available")
            {
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
                TempData["Error"] = "This car is no longer available for the selected date and time.";
                return RedirectToPage("/User/Cars/BrowseCars");
            }

            var booking = new Booking
            {
                UserId = userId.Value,
                CarId = CarId,
                StartDate = BorrowDateTime,
                EndDate = ReturnDateTime,
                TotalPrice = TotalDays * car.PricePerDay,
                Status = "pending",
                CreatedAt = DateTime.Now
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return RedirectToPage("/User/Bookings/MyBookings");
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

        private async Task<bool> IsUserReadyToBookAsync(int userId, Models.User user)
        {
            var hasProfile =
                !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                !string.IsNullOrWhiteSpace(user.Street) &&
                !string.IsNullOrWhiteSpace(user.Barangay) &&
                !string.IsNullOrWhiteSpace(user.City) &&
                !string.IsNullOrWhiteSpace(user.Country) &&
                user.Birthday != null;

            if (!hasProfile)
            {
                return false;
            }

            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .ToListAsync();

            var hasDriverLicense = documents.Any(d => d.DocType == "driver_license");
            var hasSecondaryId = documents.Any(d =>
                d.DocType == "national_id" ||
                d.DocType == "police_clearance" ||
                d.DocType == "nbi_clearance" ||
                d.DocType == "philhealth_id" ||
                d.DocType == "sss_id" ||
                d.DocType == "umid" ||
                d.DocType == "voters_id" ||
                d.DocType == "company_id");

            var hasPassport = documents.Any(d => d.DocType == "passport");
            var hasInternationalPermit = documents.Any(d => d.DocType == "international_driving_permit");

            var isForeign = user.Country.ToLower() != "philippines";

            if (isForeign)
            {
                return hasPassport && hasInternationalPermit;
            }

            return hasDriverLicense && hasSecondaryId;
        }
    }
}