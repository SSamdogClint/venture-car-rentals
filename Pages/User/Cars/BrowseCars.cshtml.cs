using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Cars
{
    public class BrowseCarsModel : PageModel
    {
        private readonly AppDbContext _context;

        public BrowseCarsModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Car> Cars { get; set; } = new();

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

        public bool IsSearchMode { get; set; }

        public string PageHeading { get; set; } = "Popular Cars";

        public string? ErrorMessage { get; set; }

        /*
            VerificationStatus possible values:

            not_logged_in        = user is not logged in
            needs_requirements   = user has not completed profile/documents yet
            pending              = user submitted documents but admin has not approved yet
            verified             = user documents are approved
        */
        public string VerificationStatus { get; set; } = "not_logged_in";

        public async Task OnGetAsync()
        {
            await CheckUserVerificationStatusAsync();

            IsSearchMode = HasCompleteDateTimeInput();

            if (!IsSearchMode)
            {
                PageHeading = "Popular Cars";

                Cars = await _context.Cars
                    .Where(c => c.Status == "available")
                    .OrderByDescending(c => _context.Bookings.Count(b => b.CarId == c.CarId))
                    .ThenByDescending(c => c.CreatedAt)
                    .ToListAsync();

                return;
            }

            var borrowValue = $"{BorrowDate} {BorrowTime}";
            var returnValue = $"{ReturnDate} {ReturnTime}";

            if (!DateTime.TryParse(borrowValue, out DateTime borrowDateTime) ||
                !DateTime.TryParse(returnValue, out DateTime returnDateTime))
            {
                ErrorMessage = "Invalid date or time format.";
                PageHeading = "Available Vehicles";
                return;
            }

            if (borrowDateTime >= returnDateTime)
            {
                ErrorMessage = "Return date and time must be after borrow date and time.";
                PageHeading = "Available Vehicles";
                return;
            }

            BorrowDateTime = borrowDateTime;
            ReturnDateTime = returnDateTime;

            var totalHours = (ReturnDateTime - BorrowDateTime).TotalHours;
            TotalDays = Math.Ceiling(totalHours / 24);

            PageHeading = "Available Vehicles";

            Cars = await _context.Cars
                .Where(car => car.Status == "available")
                .Where(car => !_context.Bookings.Any(booking =>
                    booking.CarId == car.CarId &&
                    booking.Status != "cancelled" &&
                    borrowDateTime < booking.EndDate &&
                    returnDateTime > booking.StartDate
                ))
                .OrderBy(car => car.PricePerDay)
                .ToListAsync();
        }

        private bool HasCompleteDateTimeInput()
        {
            return !string.IsNullOrWhiteSpace(BorrowDate) &&
                   !string.IsNullOrWhiteSpace(BorrowTime) &&
                   !string.IsNullOrWhiteSpace(ReturnDate) &&
                   !string.IsNullOrWhiteSpace(ReturnTime);
        }

        private async Task CheckUserVerificationStatusAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                VerificationStatus = "not_logged_in";
                return;
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                VerificationStatus = "not_logged_in";
                return;
            }

            VerificationStatus = await GetVerificationStatusAsync(user.UserId, user);
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
                var hasPassport = documents.Any(d =>
                    d.DocType == "passport");

                var hasInternationalPermit = documents.Any(d =>
                    d.DocType == "international_driving_permit");

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

                if (approvedPassport && approvedInternationalPermit)
                {
                    return "verified";
                }

                return "pending";
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

                var hasDriverLicense = documents.Any(d =>
                    d.DocType == "driver_license");

                var hasSecondaryId = documents.Any(d =>
                    secondaryDocTypes.Contains(d.DocType));

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

                if (approvedDriverLicense && approvedSecondaryId)
                {
                    return "verified";
                }

                return "pending";
            }
        }
    }
}