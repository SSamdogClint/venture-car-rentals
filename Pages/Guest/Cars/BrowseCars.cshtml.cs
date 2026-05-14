using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Guest.Cars
{
    public class BrowseCarsModel : PageModel
    {
        private readonly AppDbContext _context;

        public BrowseCarsModel(AppDbContext context)
        {
            _context = context;
        }

        public List<GuestCarBrowseViewModel> Cars { get; set; } = new();

        public List<string> FilterCategories { get; set; } = new();
        public List<string> FilterTransmissions { get; set; } = new();

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

        public string PageHeading { get; set; } = "Browse Cars";

        public string? ErrorMessage { get; set; }

        public bool HasActiveFilters =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            !string.IsNullOrWhiteSpace(CategoryFilter) ||
            !string.IsNullOrWhiteSpace(TransmissionFilter) ||
            MinSeats != null ||
            MaxPrice != null ||
            SortBy != "popular";

        public async Task OnGetAsync()
        {
            /*
                // IMPORTANT SS THIS:
                This is the guest/public Browse Cars page.

                No session check is needed here because guests are allowed
                to view cars without logging in.
            */
            await LoadFilterOptionsAsync();

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

            /*
                // IMPORTANT SS THIS:
                Partial days count as one full rental day.

                Example:
                25 hours = 2 rental days.
            */
            TotalDays = Math.Ceiling((ReturnDateTime - BorrowDateTime).TotalHours / 24);

            PageHeading = "Available Vehicles";

            /*
                // IMPORTANT SS THIS:
                Guest can search available cars by schedule.

                This prevents showing cars that already have booking conflicts
                for the selected borrow/return date.
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

                _ => query
                    .OrderByDescending(c => _context.Bookings.Count(b => b.CarId == c.CarId))
                    .ThenByDescending(c => c.CreatedAt)
            };
        }

        private async Task<List<GuestCarBrowseViewModel>> MapCarsWithReviewsAsync(List<Car> carList)
        {
            var carIds = carList
                .Select(c => c.CarId)
                .ToList();

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Where(r => carIds.Contains(r.CarId))
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            var result = new List<GuestCarBrowseViewModel>();

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
                    .Select(r => new GuestCarReviewViewModel
                    {
                        Rating = r.Rating,
                        Comment = r.Comment,
                        CreatedAt = r.CreatedAt
                    })
                    .ToList();

                result.Add(new GuestCarBrowseViewModel
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
    }

    public class GuestCarBrowseViewModel
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

        public List<GuestCarReviewViewModel> RecentReviews { get; set; } = new();
    }

    public class GuestCarReviewViewModel
    {
        public int Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}