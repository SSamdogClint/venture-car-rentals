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

        public async Task OnGetAsync()
        {
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
    }
}