using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Admin.Cars
{
    public class CarPageModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CarPageModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public List<Car> Cars { get; set; } = new();

        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int NotAvailableCars { get; set; }
        public int AvailablePercent { get; set; }
        public int NotAvailablePercent { get; set; }

        [BindProperty]
        public Car NewCar { get; set; } = new();

        [BindProperty]
        public IFormFile? CarImage { get; set; }

        [BindProperty]
        public int CarId { get; set; }

        [BindProperty]
        public string Status { get; set; } = "";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task OnGetAsync()
        {
            await LoadCarPageDataAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            if (string.IsNullOrWhiteSpace(NewCar.Make) ||
                string.IsNullOrWhiteSpace(NewCar.Model) ||
                string.IsNullOrWhiteSpace(NewCar.Category) ||
                string.IsNullOrWhiteSpace(NewCar.Status) ||
                string.IsNullOrWhiteSpace(NewCar.Transmission))
            {
                TempData["Error"] = "Please fill in all required fields.";
                return RedirectToPage();
            }

            if (NewCar.PricePerDay <= 0)
            {
                TempData["Error"] = "Price per day must be greater than 0.";
                return RedirectToPage();
            }

            if (NewCar.Seats <= 0)
            {
                TempData["Error"] = "Seats must be greater than 0.";
                return RedirectToPage();
            }

            var allowedStatuses = new[] { "available", "booked", "maintenance", "inactive" };

            NewCar.Status = NewCar.Status.ToLower();

            if (!allowedStatuses.Contains(NewCar.Status))
            {
                TempData["Error"] = "Invalid car status.";
                return RedirectToPage();
            }

            if (CarImage != null && CarImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(CarImage.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    TempData["Error"] = "Invalid image format. Please upload JPG, PNG, or WEBP.";
                    return RedirectToPage();
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "images", "cars");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var fileName = $"car_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await CarImage.CopyToAsync(stream);
                }

                NewCar.ImageUrl = $"/images/cars/{fileName}";
            }
            else
            {
                NewCar.ImageUrl = "";
            }

            NewCar.CreatedAt = DateTime.Now;
            NewCar.Description ??= "";

            _context.Cars.Add(NewCar);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Car added successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            var car = await _context.Cars.FindAsync(CarId);

            if (car == null)
            {
                TempData["Error"] = "Car not found.";
                return RedirectToPage();
            }

            var allowedStatuses = new[] { "available", "booked", "maintenance", "inactive" };

            Status = Status.ToLower();

            if (!allowedStatuses.Contains(Status))
            {
                TempData["Error"] = "Invalid status.";
                return RedirectToPage();
            }

            car.Status = Status;
                
            await _context.SaveChangesAsync();

            TempData["Success"] = "Car status updated successfully.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var car = await _context.Cars.FindAsync(CarId);

            if (car == null)
            {
                TempData["Error"] = "Car not found.";
                return RedirectToPage();
            }

            var hasBookings = await _context.Bookings.AnyAsync(b => b.CarId == CarId);

            if (hasBookings)
            {
                TempData["Error"] = "This car has booking records. Set it to inactive instead of deleting.";
                return RedirectToPage();
            }

            if (!string.IsNullOrWhiteSpace(car.ImageUrl))
            {
                var imagePath = car.ImageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
                var fullImagePath = Path.Combine(_environment.WebRootPath, imagePath);

                if (System.IO.File.Exists(fullImagePath))
                {
                    System.IO.File.Delete(fullImagePath);
                }
            }

            _context.Cars.Remove(car);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Car deleted successfully.";
            return RedirectToPage();
        }

        private async Task LoadCarPageDataAsync()
        {
            var query = _context.Cars.AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                query = query.Where(c =>
                    c.Make.Contains(SearchTerm) ||
                    c.Model.Contains(SearchTerm) ||
                    c.Category.Contains(SearchTerm) ||
                    c.Status.Contains(SearchTerm));
            }

            Cars = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            TotalCars = await _context.Cars.CountAsync();

            AvailableCars = await _context.Cars
                .CountAsync(c => c.Status == "available");

            NotAvailableCars = TotalCars - AvailableCars;

            if (TotalCars == 0)
            {
                AvailablePercent = 0;
                NotAvailablePercent = 0;
            }
            else
            {
                AvailablePercent = (int)Math.Round((double)AvailableCars / TotalCars * 100);
                NotAvailablePercent = 100 - AvailablePercent;
            }
        }
    }
}