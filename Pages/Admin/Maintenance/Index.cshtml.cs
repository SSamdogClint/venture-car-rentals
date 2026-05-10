using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Admin.Maintenance
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<MaintenanceLog> MaintenanceLogs { get; set; } = new();

        public List<Car> AvailableCars { get; set; } = new();

        public List<Car> MaintenanceCars { get; set; } = new();

        public int OngoingCount { get; set; }

        public int CompletedCount { get; set; }

        public double TotalMaintenanceCost { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "ongoing";

        /*
            IMPORTANT:
            Start Maintenance now searches using VIN, not CarId, car name, make, or model.
        */
        [BindProperty]
        public string CarVin { get; set; } = "";

        [BindProperty]
        public int MaintenanceLogId { get; set; }

        [BindProperty]
        public string Description { get; set; } = "";

        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Now;

        [BindProperty]
        public DateTime? EndDate { get; set; }

        [BindProperty]
        public double Cost { get; set; }

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostStartMaintenanceAsync()
        {
            /*
                IMPORTANT:
                The admin enters/searches the car VIN.
                The system finds the car using Car.VIN.
            */

            if (string.IsNullOrWhiteSpace(CarVin))
            {
                TempData["Error"] = "Please enter the car VIN.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                TempData["Error"] = "Please enter a maintenance description.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (Cost < 0)
            {
                TempData["Error"] = "Cost cannot be negative.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            var normalizedVin = CarVin.Trim().ToLower();

            var car = await _context.Cars
                .FirstOrDefaultAsync(c =>
                    !string.IsNullOrWhiteSpace(c.VIN) &&
                    c.VIN.ToLower() == normalizedVin);

            if (car == null)
            {
                TempData["Error"] = "No car found with that VIN.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (SameStatus(car.Status, "booked"))
            {
                TempData["Error"] = "This car is currently booked. You cannot start maintenance while it is booked.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (SameStatus(car.Status, "maintenance"))
            {
                TempData["Error"] = "This car is already under maintenance.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (SameStatus(car.Status, "inactive"))
            {
                TempData["Error"] = "This car is inactive. Activate the car first before starting maintenance.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            var hasOngoingMaintenance = await _context.MaintenanceLogs
                .AnyAsync(m =>
                    m.CarId == car.CarId &&
                    m.MaintenanceStatus == "ongoing");

            if (hasOngoingMaintenance)
            {
                TempData["Error"] = "This car already has an ongoing maintenance record.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            var maintenanceLog = new MaintenanceLog
            {
                CarId = car.CarId,
                Description = Description.Trim(),
                MaintenanceStatus = "ongoing",
                StartDate = StartDate,
                EndDate = null,
                Cost = Cost
            };

            _context.MaintenanceLogs.Add(maintenanceLog);

            /*
                IMPORTANT:
                Once maintenance starts, the car becomes unavailable for booking.
            */
            car.Status = "maintenance";

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Maintenance started successfully for VIN {car.VIN}.";
            return RedirectToPage(new { tab = "ongoing" });
        }

        public async Task<IActionResult> OnPostCompleteMaintenanceAsync()
        {
            var maintenanceLog = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .FirstOrDefaultAsync(m => m.MaintenanceLogId == MaintenanceLogId);

            if (maintenanceLog == null)
            {
                TempData["Error"] = "Maintenance record not found.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (!SameStatus(maintenanceLog.MaintenanceStatus, "ongoing"))
            {
                TempData["Error"] = "Only ongoing maintenance can be completed.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (Cost < 0)
            {
                TempData["Error"] = "Cost cannot be negative.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            maintenanceLog.MaintenanceStatus = "completed";
            maintenanceLog.EndDate = EndDate ?? DateTime.Now;
            maintenanceLog.Cost = Cost;

            if (maintenanceLog.Car != null &&
                !SameStatus(maintenanceLog.Car.Status, "inactive"))
            {
                maintenanceLog.Car.Status = "available";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Maintenance completed successfully. Car is now available.";
            return RedirectToPage(new { tab = "completed" });
        }

        public async Task<IActionResult> OnPostCancelMaintenanceAsync()
        {
            var maintenanceLog = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .FirstOrDefaultAsync(m => m.MaintenanceLogId == MaintenanceLogId);

            if (maintenanceLog == null)
            {
                TempData["Error"] = "Maintenance record not found.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            if (!SameStatus(maintenanceLog.MaintenanceStatus, "ongoing"))
            {
                TempData["Error"] = "Only ongoing maintenance can be cancelled.";
                return RedirectToPage(new { tab = "ongoing" });
            }

            _context.MaintenanceLogs.Remove(maintenanceLog);

            if (maintenanceLog.Car != null &&
                !SameStatus(maintenanceLog.Car.Status, "inactive"))
            {
                maintenanceLog.Car.Status = "available";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Maintenance cancelled successfully. Car is now available.";
            return RedirectToPage(new { tab = "ongoing" });
        }

        private async Task LoadPageDataAsync()
        {
            Tab = NormalizeTab(Tab);

            var allLogs = await _context.MaintenanceLogs
                .Include(m => m.Car)
                .OrderByDescending(m => m.StartDate)
                .ToListAsync();

            /*
                IMPORTANT:
                AvailableCars is used for the VIN datalist suggestions.
                The admin still searches by VIN, but suggestions show make/model for reference.
            */
            AvailableCars = await _context.Cars
                .Where(c =>
                    c.Status != "booked" &&
                    c.Status != "maintenance" &&
                    c.Status != "inactive")
                .OrderBy(c => c.VIN)
                .ToListAsync();

            MaintenanceCars = await _context.Cars
                .Where(c => c.Status == "maintenance")
                .OrderBy(c => c.VIN)
                .ToListAsync();

            OngoingCount = allLogs.Count(m => SameStatus(m.MaintenanceStatus, "ongoing"));
            CompletedCount = allLogs.Count(m => SameStatus(m.MaintenanceStatus, "completed"));
            TotalMaintenanceCost = allLogs.Sum(m => m.Cost);

            MaintenanceLogs = Tab switch
            {
                "completed" => allLogs
                    .Where(m => SameStatus(m.MaintenanceStatus, "completed"))
                    .ToList(),

                "all" => allLogs,

                _ => allLogs
                    .Where(m => SameStatus(m.MaintenanceStatus, "ongoing"))
                    .ToList()
            };
        }

        private static string NormalizeTab(string? tab)
        {
            return tab?.ToLower().Trim() switch
            {
                "completed" => "completed",
                "all" => "all",
                _ => "ongoing"
            };
        }

        private static bool SameStatus(string? value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}