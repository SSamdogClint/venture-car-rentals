using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
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
            // Database context used to load cars, bookings, reviews, user profile, and user documents.
            _context = context;
        }

        // List of cars displayed on the private user Browse Cars page.
        public List<CarBrowseViewModel> Cars { get; set; } = new();

        // Filter dropdown options.
        public List<string> FilterCategories { get; set; } = new();
        public List<string> FilterTransmissions { get; set; } = new();

        // Schedule query string values from User/Home search form.
        [BindProperty(SupportsGet = true)]
        public string? BorrowDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowTime { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTime { get; set; }

        // Filter query string values.
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

        // Parsed date/time values used for booking availability checking.
        public DateTime BorrowDateTime { get; set; }
        public DateTime ReturnDateTime { get; set; }

        // Rental duration based on selected borrow and return schedule.
        public double TotalDays { get; set; }

        /*
            IMPORTANT SS THIS:
            IsSearchMode means the user already selected borrow and return schedule.

            false:
                Show normal available cars.

            true:
                Show cars available for the selected schedule and allow booking flow.
        */
        public bool IsSearchMode { get; set; }

        // Page title text.
        public string PageHeading { get; set; } = "Browse Cars";

        // Error message shown when date/time input is invalid.
        public string? ErrorMessage { get; set; }

        /*
            // IMPORTANT SS THIS:
            These are the possible verification statuses for a logged-in user.

            needs_requirements:
                User profile or required documents are incomplete.

            pending:
                Documents are uploaded but not fully approved yet.

            verified:
                User can proceed to booking.

            rejected:
                User must update rejected documents.

            expired:
                User must renew expired documents.

            underage:
                User is below 18 years old and cannot book.
        */
        public string VerificationStatus { get; set; } = "needs_requirements";

        /*
            // IMPORTANT SS THIS:
            This controls whether the filter panel should stay open.

            Example:
            If the user searched "Toyota" or selected a category,
            the filter panel remains open after page reload.
        */
        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            !string.IsNullOrWhiteSpace(CategoryFilter) ||
            !string.IsNullOrWhiteSpace(TransmissionFilter) ||
            MinSeats != null ||
            MaxPrice != null ||
            SortBy != "popular";

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            /*
                // IMPORTANT SS THIS:
                This is the PRIVATE user Browse Cars page.

                Guests should not use this page anymore.
                Guests should use:
                    /Guest/Cars/BrowseCars

                This check is only a safety fallback.
                Your SessionAuthFilter should also protect /User pages.
            */
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadFilterOptionsAsync();

            // Check if the logged-in user is allowed to book.
            VerificationStatus = await GetVerificationStatusAsync(userId.Value);

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

                Cars = await MapCarsWithReviewsAsync(carList);

                return Page();
            }

            var borrowValue = $"{BorrowDate} {BorrowTime}";
            var returnValue = $"{ReturnDate} {ReturnTime}";

            if (!DateTime.TryParse(borrowValue, out DateTime borrowDateTime) ||
                !DateTime.TryParse(returnValue, out DateTime returnDateTime))
            {
                ErrorMessage = "Invalid date or time format.";
                PageHeading = "Available Vehicles";
                return Page();
            }

            if (borrowDateTime >= returnDateTime)
            {
                ErrorMessage = "Return date and time must be after borrow date and time.";
                PageHeading = "Available Vehicles";
                return Page();
            }

            BorrowDateTime = borrowDateTime;
            ReturnDateTime = returnDateTime;

            /*
                // IMPORTANT SS THIS:
                Partial rental days count as a full day.

                Example:
                25 rental hours = 2 rental days.
            */
            var totalHours = (ReturnDateTime - BorrowDateTime).TotalHours;
            TotalDays = Math.Ceiling(totalHours / 24);

            PageHeading = "Available Vehicles";

            /*
                // IMPORTANT SS THIS:
                This query prevents double booking.

                A car is shown only if:
                - car is available
                - no existing non-cancelled booking overlaps with the selected schedule
            */
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

            Cars = await MapCarsWithReviewsAsync(availableCars);

            return Page();
        }

        private async Task LoadFilterOptionsAsync()
        {
            // Loads available car categories for the filter dropdown.
            FilterCategories = await _context.Cars
                .AsNoTracking()
                .Where(c => c.Status == "available" && c.Category != "")
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            // Loads available car transmission types for the filter dropdown.
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

            return SortBy switch
            {
                "lowest_price" => query.OrderBy(c => c.PricePerDay),
                "highest_price" => query.OrderByDescending(c => c.PricePerDay),
                "newest" => query.OrderByDescending(c => c.CreatedAt),

                /*
                    // IMPORTANT SS THIS:
                    Default sorting is popular.

                    Popular means cars with more bookings appear first.
                */
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

        private async Task<string> GetVerificationStatusAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return "needs_requirements";
            }

            /*
                // IMPORTANT SS THIS:
                User must have a birthday and must be at least 18 years old.

                This is required before allowing car rental booking.
            */
            if (user.Birthday == null)
            {
                return "needs_requirements";
            }

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

            /*
                // IMPORTANT SS THIS:
                User becomes verified only if all required documents are approved
                and none are expired.
            */
            if (requiredInfo.SubmittedCount == requiredInfo.RequiredTotal &&
                requiredInfo.ApprovedCount == requiredInfo.RequiredTotal)
            {
                return "verified";
            }

            return "pending";
        }

        private BrowseRequiredDocumentInfo GetRequiredDocumentInfo(AppUser user, List<UserDocument> documents)
        {
            var requiredDocuments = new List<UserDocument>();

            var userCountry = user.Country?.ToLower() ?? "";
            var isForeign = userCountry != "philippines";

            if (isForeign)
            {
                /*
                    // IMPORTANT SS THIS:
                    Foreign renters require:
                    - Passport
                    - International Driving Permit
                */
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

                return new BrowseRequiredDocumentInfo
                {
                    RequiredTotal = 2,
                    SubmittedCount = requiredDocuments.Count,
                    ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                    HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                    HasExpired = requiredDocuments.Any(IsExpired),
                    HasAnySubmitted = requiredDocuments.Any()
                };
            }

            /*
                // IMPORTANT SS THIS:
                Local renters require:
                - Driver's License
                - One secondary valid ID
            */
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

            return new BrowseRequiredDocumentInfo
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
            var userCountry = user.Country?.ToLower() ?? "";
            var isForeign = userCountry != "philippines";

            /*
                // IMPORTANT SS THIS:
                Foreign renters do not need Street, Barangay, and State.

                Required for foreign renters:
                - Phone number
                - City
                - Zip code
                - Country
                - Birthday
            */
            if (isForeign)
            {
                return !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                       !string.IsNullOrWhiteSpace(user.City) &&
                       !string.IsNullOrWhiteSpace(user.ZipCode) &&
                       !string.IsNullOrWhiteSpace(user.Country) &&
                       user.Birthday != null;
            }

            /*
                // IMPORTANT SS THIS:
                Local renters must complete full local address information.
            */
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

    public class BrowseRequiredDocumentInfo
    {
        public int RequiredTotal { get; set; }
        public int SubmittedCount { get; set; }
        public int ApprovedCount { get; set; }
        public bool HasRejected { get; set; }
        public bool HasExpired { get; set; }
        public bool HasAnySubmitted { get; set; }
    }
}