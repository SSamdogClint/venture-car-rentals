using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

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
        public double LastWeekIncome { get; set; }
        public double IncomePercent { get; set; }

        public double TodayMaintenance { get; set; }
        public double LastWeekMaintenance { get; set; }
        public double MaintenancePercent { get; set; }

        public int RentPercent { get; set; }
        public int CancelPercent { get; set; }
        public int PendingPercent { get; set; }

        public List<CarOptionViewModel> CarOptions { get; set; } = new();
        public List<LiveCarStatusViewModel> LiveCars { get; set; } = new();
        public List<BookingSummaryViewModel> BookingSummary { get; set; } = new();

        // Full car records used by the reused car details modal.
        public List<Car> CarDetails { get; set; } = new();

        public string BookingRangeLabel { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public int? SelectedCarId { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime SelectedDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedTime { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task OnGetAsync()
        {
            if (SelectedDate == default)
            {
                SelectedDate = DateTime.Today;
            }

            if (string.IsNullOrWhiteSpace(SelectedTime))
            {
                SelectedTime = DateTime.Now.ToString("HH:mm");
            }

            await LoadDashboardDataAsync();
        }

        private async Task LoadDashboardDataAsync()
        {
            var cars = await LoadCarsAsync();
            var bookings = await _context.Bookings.ToListAsync();

            CarOptions = cars
                .Select(car => new CarOptionViewModel
                {
                    CarId = car.CarId,
                    CarNumber = car.CarId.ToString("0000"),
                    CarName = $"{car.Make} {car.Model}"
                })
                .ToList();

            CarDetails = cars;

            LoadIncomeStatistics(bookings);
            LoadMaintenanceStatistics();
            LoadRentCancelPendingPercentages(bookings);
            LoadLiveCarStatus(cars, bookings);
            LoadBookingSummary(bookings);
        }

        private async Task<List<Car>> LoadCarsAsync()
        {
            var query = _context.Cars.AsQueryable();

            if (SelectedCarId != null)
            {
                query = query.Where(c => c.CarId == SelectedCarId.Value);
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(c =>
                    c.Make.Contains(SearchTerm) ||
                    c.Model.Contains(SearchTerm) ||
                    c.Category.Contains(SearchTerm) ||
                    c.Status.Contains(SearchTerm) ||
                    c.Color.Contains(SearchTerm) ||
                    c.LicensePlate.Contains(SearchTerm) ||
                    c.VIN.Contains(SearchTerm));
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
        }

        private void LoadIncomeStatistics(List<Booking> bookings)
        {
            /*
                IMPORTANT FEATURE:
                Income counts approved/completed bookings only.
                Pending bookings are not real income yet, so they are excluded.
            */

            var todayStart = DateTime.Today;
            var tomorrowStart = todayStart.AddDays(1);

            var lastWeekStart = todayStart.AddDays(-7);
            var lastWeekEnd = todayStart;

            TodayIncome = bookings
                .Where(b =>
                    IsIncomeBookingStatus(b.Status) &&
                    b.CreatedAt >= todayStart &&
                    b.CreatedAt < tomorrowStart)
                .Sum(b => b.TotalPrice);

            LastWeekIncome = bookings
                .Where(b =>
                    IsIncomeBookingStatus(b.Status) &&
                    b.CreatedAt >= lastWeekStart &&
                    b.CreatedAt < lastWeekEnd)
                .Sum(b => b.TotalPrice);

            IncomePercent = ComputePercentChange(TodayIncome, LastWeekIncome);
        }

        private void LoadMaintenanceStatistics()
        {
            /*
                Maintenance is set to 0 here because maintenance expense calculation
                depends on your MaintenanceLog model fields.
                You can connect this later if your MaintenanceLog has Cost and CreatedAt.
            */

            TodayMaintenance = 0;
            LastWeekMaintenance = 0;
            MaintenancePercent = 0;
        }

        private void LoadRentCancelPendingPercentages(List<Booking> bookings)
        {
            var totalBookings = bookings.Count;

            if (totalBookings == 0)
            {
                RentPercent = 0;
                CancelPercent = 0;
                PendingPercent = 0;
                return;
            }

            var rentCount = bookings.Count(b => IsIncomeBookingStatus(b.Status));
            var cancelCount = bookings.Count(b => b.Status == "cancelled");
            var pendingCount = bookings.Count(b => b.Status == "pending");

            RentPercent = (int)Math.Round((double)rentCount / totalBookings * 100);
            CancelPercent = (int)Math.Round((double)cancelCount / totalBookings * 100);
            PendingPercent = (int)Math.Round((double)pendingCount / totalBookings * 100);
        }

        private void LoadLiveCarStatus(List<Car> cars, List<Booking> bookings)
        {
            var selectedDateTime = BuildSelectedDateTime();

            LiveCars = cars.Select(car =>
            {
                var bookingAtSelectedTime = bookings
                    .Where(b =>
                        b.CarId == car.CarId &&
                        b.Status != "cancelled" &&
                        selectedDateTime >= b.StartDate &&
                        selectedDateTime <= b.EndDate)
                    .OrderByDescending(b => b.CreatedAt)
                    .FirstOrDefault();

                var approvedCarIncome = bookings
                    .Where(b =>
                        b.CarId == car.CarId &&
                        IsIncomeBookingStatus(b.Status))
                    .Sum(b => b.TotalPrice);

                var statusText = GetDisplayStatus(car, bookingAtSelectedTime);

                var showIncome = false;
                var incomeValue = 0.0;
                var incomeNote = "No approved income";

                /*
                    IMPORTANT FEATURE:
                    If the selected/current booking is pending, the dashboard hides the money value.
                    This prevents pending bookings from being displayed as real income.
                */
                if (bookingAtSelectedTime != null)
                {
                    if (IsIncomeBookingStatus(bookingAtSelectedTime.Status))
                    {
                        showIncome = true;
                        incomeValue = bookingAtSelectedTime.TotalPrice;
                    }
                    else
                    {
                        showIncome = false;
                        incomeNote = "Pending approval";
                    }
                }
                else if (approvedCarIncome > 0)
                {
                    showIncome = true;
                    incomeValue = approvedCarIncome;
                }

                return new LiveCarStatusViewModel
                {
                    CarId = car.CarId,
                    CarNumber = car.CarId.ToString("0000"),
                    CarName = $"{car.Make} {car.Model}",
                    Status = statusText,
                    Earning = incomeValue,
                    ShowIncome = showIncome,
                    IncomeNote = incomeNote
                };
            }).ToList();
        }

        private void LoadBookingSummary(List<Booking> bookings)
        {
            var currentYear = DateTime.Today.Year;
            BookingRangeLabel = $"Monthly summary for {currentYear}";

            var monthlyCounts = Enumerable.Range(1, 12)
                .Select(month => new
                {
                    MonthNumber = month,
                    Count = bookings.Count(b =>
                        b.CreatedAt.Year == currentYear &&
                        b.CreatedAt.Month == month)
                })
                .ToList();

            var maxCount = monthlyCounts.Max(m => m.Count);

            BookingSummary = monthlyCounts.Select(item =>
            {
                var barHeight = maxCount == 0
                    ? 5
                    : Math.Max(5, (int)Math.Round((double)item.Count / maxCount * 100));

                return new BookingSummaryViewModel
                {
                    Month = new DateTime(currentYear, item.MonthNumber, 1).ToString("MMM"),
                    Count = item.Count,
                    BarHeight = barHeight
                };
            }).ToList();
        }

        private DateTime BuildSelectedDateTime()
        {
            if (TimeSpan.TryParse(SelectedTime, out var selectedTimeSpan))
            {
                return SelectedDate.Date.Add(selectedTimeSpan);
            }

            return SelectedDate.Date;
        }

        private string GetDisplayStatus(Car car, Booking? booking)
        {
            if (booking == null)
            {
                return car.Status;
            }

            if (booking.Status == "approved")
            {
                return "Approved Booking";
            }

            if (booking.Status == "completed")
            {
                return "Completed Booking";
            }

            if (booking.Status == "pending")
            {
                return "Pending Approval";
            }

            return booking.Status;
        }

        private bool IsIncomeBookingStatus(string status)
        {
            return status == "approved" || status == "completed";
        }

        private double ComputePercentChange(double currentValue, double previousValue)
        {
            if (previousValue <= 0)
            {
                return currentValue > 0 ? 100 : 0;
            }

            return (currentValue - previousValue) / previousValue * 100;
        }
    }

    public class CarOptionViewModel
    {
        public int CarId { get; set; }
        public string CarNumber { get; set; } = "";
        public string CarName { get; set; } = "";
    }

    public class LiveCarStatusViewModel
    {
        public int CarId { get; set; }
        public string CarNumber { get; set; } = "";
        public string CarName { get; set; } = "";
        public string Status { get; set; } = "";
        public double Earning { get; set; }
        public bool ShowIncome { get; set; }
        public string IncomeNote { get; set; } = "";
    }

    public class BookingSummaryViewModel
    {
        public string Month { get; set; } = "";
        public int Count { get; set; }
        public int BarHeight { get; set; }
    }
}