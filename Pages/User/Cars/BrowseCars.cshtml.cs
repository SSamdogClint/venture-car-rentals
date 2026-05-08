using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

// This alias avoids conflict with the namespace VentureCarRentals.Pages.User.
using AppUser = VentureCarRentals.Models.User;

namespace VentureCarRentals.Pages.User.Cars
{
    public class BrowseCarsModel : PageModel
    {
        private readonly AppDbContext _context;

        public BrowseCarsModel(AppDbContext context)
        {
            _context = context;
        }

        // This now uses a view model so every car can include review/rating data.
        public List<CarBrowseViewModel> Cars { get; set; } = new();

        public List<string> FilterCategories { get; set; } = new();
        public List<string> FilterTransmissions { get; set; } = new();

        public bool IsLoggedIn { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowTime { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTime { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TransmissionFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? MinSeats { get; set; }

        [BindProperty(SupportsGet = true)]
        public double? MaxPrice { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "popular";

        public DateTime BorrowDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }

        public double TotalDays { get; set; }

        public bool IsSearchMode { get; set; }

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            !string.IsNullOrWhiteSpace(CategoryFilter) ||
            !string.IsNullOrWhiteSpace(TransmissionFilter) ||
            MinSeats != null ||
            MaxPrice != null ||
            SortBy != "popular";

        public string PageHeading { get; set; } = "Browse Cars";

        public string? ErrorMessage { get; set; }

        /*
            VerificationStatus possible values:

            not_logged_in        = user is not logged in
            needs_requirements   = user has incomplete profile or missing required documents
            pending              = user submitted some/all requirements but not fully approved yet
            verified             = all required documents are approved
            rejected             = one required document was rejected
            expired              = one required document is expired
        */
        public string VerificationStatus { get; set; } = "not_logged_in";

        public async Task OnGetAsync()
        {
            // Checks if the visitor is logged in.
            IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            await LoadFilterOptionsAsync();
            await CheckUserVerificationStatusAsync();

            IsSearchMode = HasCompleteDateTimeInput();

            if (!IsSearchMode)
            {
                PageHeading = "Browse Cars";

                var query = _context.Cars
                    .AsNoTracking()
                    .Where(c => c.Status == "available");

                query = ApplyCarFilters(query);
                query = ApplyCarSorting(query);

                var carList = await query.ToListAsync();

                // IMPORTANT:
                // Adds average rating, review count, and recent review comments to every car.
                Cars = await MapCarsWithReviewsAsync(carList);

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

            var availableQuery = _context.Cars
                .AsNoTracking()
                .Where(car => car.Status == "available")
                .Where(car => !_context.Bookings.Any(booking =>
                    booking.CarId == car.CarId &&
                    booking.Status != "cancelled" &&
                    borrowDateTime < booking.EndDate &&
                    returnDateTime > booking.StartDate
                ));

            availableQuery = ApplyCarFilters(availableQuery);
            availableQuery = ApplyCarSorting(availableQuery);

            var availableCars = await availableQuery.ToListAsync();

            // IMPORTANT:
            // Available cars still include real review data.
            Cars = await MapCarsWithReviewsAsync(availableCars);
        }

        private async Task LoadFilterOptionsAsync()
        {
            FilterCategories = await _context.Cars
                .AsNoTracking()
                .Where(c => c.Status == "available" && c.Category != "")
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            FilterTransmissions = await _context.Cars
                .AsNoTracking()
                .Where(c => c.Status == "available" && c.Transmission != "")
                .Select(c => c.Transmission)
                .Distinct()
                .OrderBy(t => t)
                .ToListAsync();
        }

        private IQueryable<Car> ApplyCarFilters(IQueryable<Car> query)
        {
            // Filters work in both public browse mode and date/time availability mode.
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim().ToLower();

                query = query.Where(c =>
                    c.Make.ToLower().Contains(keyword) ||
                    c.Model.ToLower().Contains(keyword) ||
                    c.Category.ToLower().Contains(keyword) ||
                    c.Transmission.ToLower().Contains(keyword) ||
                    c.Color.ToLower().Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(CategoryFilter))
            {
                var category = CategoryFilter.Trim();

                query = query.Where(c => c.Category == category);
            }

            if (!string.IsNullOrWhiteSpace(TransmissionFilter))
            {
                var transmission = TransmissionFilter.Trim();

                query = query.Where(c => c.Transmission == transmission);
            }

            if (MinSeats != null)
            {
                query = query.Where(c => c.Seats >= MinSeats.Value);
            }

            if (MaxPrice != null)
            {
                query = query.Where(c => c.PricePerDay <= MaxPrice.Value);
            }

            return query;
        }

        private IQueryable<Car> ApplyCarSorting(IQueryable<Car> query)
        {
            SortBy = string.IsNullOrWhiteSpace(SortBy)
                ? "popular"
                : SortBy.ToLower().Trim();

            /*
                Popular uses booking count.
                Cars with more bookings appear first.
            */
            return SortBy switch
            {
                "lowest_price" => query.OrderBy(c => c.PricePerDay),
                "highest_price" => query.OrderByDescending(c => c.PricePerDay),
                "newest" => query.OrderByDescending(c => c.CreatedAt),

                _ => query
                    .OrderByDescending(c => _context.Bookings.Count(b => b.CarId == c.CarId))
                    .ThenByDescending(c => c.CreatedAt)
            };
        }

        private async Task<List<CarBrowseViewModel>> MapCarsWithReviewsAsync(List<Car> carList)
        {
            var carIds = carList
                .Select(c => c.CarId)
                .ToList();

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => carIds.Contains(r.CarId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var result = new List<CarBrowseViewModel>();

            foreach (var car in carList)
            {
                var carReviews = reviews
                    .Where(r => r.CarId == car.CarId)
                    .ToList();

                var reviewCount = carReviews.Count;

                var averageRating = reviewCount == 0
                    ? 0
                    : Math.Round(carReviews.Average(r => r.Rating), 1);

                var recentReviews = carReviews
                    .Take(3)
                    .Select(r => new CarReviewViewModel
                    {
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt
                    })
                    .ToList();

                result.Add(new CarBrowseViewModel
                {
                    CarId = car.CarId,
                    Make = car.Make,
                    Model = car.Model,
                    Year = car.Year,
                    Category = car.Category,
                    PricePerDay = car.PricePerDay,
                    Status = car.Status,
                    Seats = car.Seats,
                    Transmission = car.Transmission,
                    Description = car.Description,
                    ImageUrl = car.ImageUrl,
                    Color = car.Color,

                    // Do not include LicensePlate and VIN here because Browse Cars is user/public side.

                    AverageRating = averageRating,
                    ReviewCount = reviewCount,
                    RecentReviews = recentReviews
                });
            }

            return result;
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

        private async Task<string> GetVerificationStatusAsync(int userId, AppUser user)
        {
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

        private RequiredDocumentInfo GetRequiredDocumentInfo(AppUser user, List<UserDocument> documents)
        {
            /*
                IMPORTANT FEATURE:
                User can book only when ALL required documents are approved.
                If only one document is approved, the status remains pending.
            */

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

                return new RequiredDocumentInfo
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

            return new RequiredDocumentInfo
            {
                RequiredTotal = 2,
                SubmittedCount = requiredDocuments.Count,
                ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                HasExpired = requiredDocuments.Any(IsExpired),
                HasAnySubmitted = requiredDocuments.Any()
            };
        }

        private bool IsProfileComplete(AppUser user)
        {
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

    public class CarBrowseViewModel
    {
        public int CarId { get; set; }

        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public int Year { get; set; }
        public string Category { get; set; } = "";

        public double PricePerDay { get; set; }

        public string Status { get; set; } = "";
        public int Seats { get; set; }
        public string Transmission { get; set; } = "";
        public string Description { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Color { get; set; } = "";

        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public List<CarReviewViewModel> RecentReviews { get; set; } = new();
    }

    public class CarReviewViewModel
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RequiredDocumentInfo
    {
        public int RequiredTotal { get; set; }
        public int SubmittedCount { get; set; }
        public int ApprovedCount { get; set; }
        public bool HasRejected { get; set; }
        public bool HasExpired { get; set; }
        public bool HasAnySubmitted { get; set; }
    }
}