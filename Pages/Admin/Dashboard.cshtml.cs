using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly AppDbContext _context;

        public DashboardModel(AppDbContext context)
        {
            _context = context;
        }

        public double TodayIncome { get; set; }
        public double IncomePercent { get; set; }
        public double LastWeekIncome { get; set; }

        public double TodayMaintenance { get; set; }
        public double MaintenancePercent { get; set; }
        public double LastWeekMaintenance { get; set; }

        public int RentPercent { get; set; }
        public int CancelPercent { get; set; }
        public int PendingPercent { get; set; }

        public List<CarOptionViewModel> CarOptions { get; set; } = new();
        public List<LiveCarViewModel> LiveCars { get; set; } = new();

        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string SelectedTime { get; set; } = "10 AM";

        public string BookingRangeLabel { get; set; } = "Last 6 months";
        public List<BookingSummaryViewModel> BookingSummary { get; set; } = new();

        public async Task OnGetAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var lastWeekStart = today.AddDays(-7);
            var lastWeekEnd = today;

            TodayIncome = await _context.Payments
                .Where(p => p.PaymentStatus == "paid"
                    && p.PaidAt != null
                    && p.PaidAt >= today
                    && p.PaidAt < tomorrow)
                .SumAsync(p => p.Amount);

            LastWeekIncome = await _context.Payments
                .Where(p => p.PaymentStatus == "paid"
                    && p.PaidAt != null
                    && p.PaidAt >= lastWeekStart
                    && p.PaidAt < lastWeekEnd)
                .SumAsync(p => p.Amount);

            TodayMaintenance = await _context.MaintenanceLogs
                .Where(m => m.StartDate >= today && m.StartDate < tomorrow)
                .SumAsync(m => m.Cost);

            LastWeekMaintenance = await _context.MaintenanceLogs
                .Where(m => m.StartDate >= lastWeekStart && m.StartDate < lastWeekEnd)
                .SumAsync(m => m.Cost);

            IncomePercent = CalculatePercent(TodayIncome, LastWeekIncome);
            MaintenancePercent = CalculatePercent(TodayMaintenance, LastWeekMaintenance);

            var totalBookings = await _context.Bookings.CountAsync();

            var completedBookings = await _context.Bookings
                .CountAsync(b => b.Status == "completed" || b.Status == "confirmed");

            var cancelledBookings = await _context.Bookings
                .CountAsync(b => b.Status == "cancelled");

            var pendingBookings = await _context.Bookings
                .CountAsync(b => b.Status == "pending");

            RentPercent = GetPercent(completedBookings, totalBookings);
            CancelPercent = GetPercent(cancelledBookings, totalBookings);
            PendingPercent = GetPercent(pendingBookings, totalBookings);

            var cars = await _context.Cars
                .OrderBy(c => c.Make)
                .ToListAsync();

            CarOptions = cars.Select(c => new CarOptionViewModel
            {
                CarId = c.CarId,
                CarNumber = c.CarId.ToString("0000"),
                CarName = c.Make + " " + c.Model
            }).ToList();

            LiveCars = cars
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new LiveCarViewModel
                {
                    CarId = c.CarId,
                    CarNumber = c.CarId.ToString("0000"),
                    CarName = c.Make + " " + c.Model,
                    Status = c.Status,
                    Earning = _context.Bookings
                        .Where(b => b.CarId == c.CarId && b.Status != "cancelled")
                        .Sum(b => b.TotalPrice)
                })
                .ToList();

            BookingSummary = new List<BookingSummaryViewModel>
            {
                new BookingSummaryViewModel { Month = "Sep", BarHeight = 55 },
                new BookingSummaryViewModel { Month = "Oct", BarHeight = 70 },
                new BookingSummaryViewModel { Month = "Nov", BarHeight = 45 },
                new BookingSummaryViewModel { Month = "Dec", BarHeight = 60 },
                new BookingSummaryViewModel { Month = "Jan", BarHeight = 75 },
                new BookingSummaryViewModel { Month = "Feb", BarHeight = 65 }
            };
        }

        private static double CalculatePercent(double current, double previous)
        {
            if (previous <= 0)
            {
                return current > 0 ? 100 : 0;
            }

            return Math.Round(((current - previous) / previous) * 100, 1);
        }

        private static int GetPercent(int value, int total)
        {
            if (total == 0)
            {
                return 0;
            }

            return (int)Math.Round((double)value / total * 100);
        }
    }

    public class CarOptionViewModel
    {
        public int CarId { get; set; }
        public string CarNumber { get; set; } = "";
        public string CarName { get; set; } = "";
    }

    public class LiveCarViewModel
    {
        public int CarId { get; set; }
        public string CarNumber { get; set; } = "";
        public string CarName { get; set; } = "";
        public string Status { get; set; } = "";
        public double Earning { get; set; }
    }

    public class BookingSummaryViewModel
    {
        public string Month { get; set; } = "";
        public int BarHeight { get; set; }
    }
}