using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Admin.Cars
{
    public class CarPageModel : PageModel
    {
        // Database context used to access the database tables.
        private readonly AppDbContext _context;

        // Environment service used for saving and deleting files inside wwwroot.
        private readonly IWebHostEnvironment _environment;

        public CarPageModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // List of cars displayed in the admin car table.
        public List<Car> Cars { get; set; } = new();

        // Statistics for the left dashboard cards.
        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int NotAvailableCars { get; set; }
        public int AvailablePercent { get; set; }
        public int NotAvailablePercent { get; set; }

        // Binds the Add Car form.
        [BindProperty]
        public Car NewCar { get; set; } = new();

        // Binds the uploaded image in Add Car modal.
        [BindProperty]
        public IFormFile? CarImage { get; set; }

        // Binds selected car ID for status update and delete.
        [BindProperty]
        public int CarId { get; set; }

        // Binds quick status update field.
        [BindProperty]
        public string Status { get; set; } = "";

        // Binds selected car ID for edit.
        [BindProperty]
        public int EditCarId { get; set; }

        // Binds edited car information.
        [BindProperty] public string EditMake { get; set; } = "";
        [BindProperty] public string EditModel { get; set; } = "";
        [BindProperty] public int EditYear { get; set; }
        [BindProperty] public string EditColor { get; set; } = "";
        [BindProperty] public string EditCategory { get; set; } = "";
        [BindProperty] public double EditPricePerDay { get; set; }
        [BindProperty] public int EditSeats { get; set; }
        [BindProperty] public string EditTransmission { get; set; } = "";
        [BindProperty] public string EditLicensePlate { get; set; } = "";
        [BindProperty] public string EditVIN { get; set; } = "";
        [BindProperty] public string EditDescription { get; set; } = "";

        // Binds replacement image in Edit Car modal.
        [BindProperty]
        public IFormFile? EditCarImage { get; set; }

        // Binds search keyword from query string.
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task OnGetAsync()
        {
            // Load car list and dashboard statistics when admin opens the page.
            await LoadCarPageDataAsync();
        }

        public async Task<IActionResult> OnPostAddAsync()
        {
            /*
                IMPORTANT FEATURE:
                Add Car validates complete vehicle information before saving.
                This ensures admin records are complete and ready for rental agreement generation.
            */

            var validationError = ValidateCarInformationInput(
                NewCar.Make,
                NewCar.Model,
                NewCar.Year,
                NewCar.Color,
                NewCar.Category,
                NewCar.PricePerDay,
                NewCar.Seats,
                NewCar.Transmission,
                NewCar.LicensePlate,
                NewCar.VIN
            );

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                TempData["Error"] = validationError;
                return RedirectToPage();
            }

            // Validate selected car status for new car.
            var statusError = ValidateStatus(NewCar.Status);

            if (!string.IsNullOrWhiteSpace(statusError))
            {
                TempData["Error"] = statusError;
                return RedirectToPage();
            }

            // Normalize text before saving.
            NewCar.Make = NewCar.Make.Trim();
            NewCar.Model = NewCar.Model.Trim();
            NewCar.Color = NewCar.Color.Trim();
            NewCar.Category = NewCar.Category.Trim();
            NewCar.Status = NewCar.Status.ToLower().Trim();
            NewCar.Transmission = NewCar.Transmission.Trim();
            NewCar.LicensePlate = NewCar.LicensePlate.Trim().ToUpper();
            NewCar.VIN = NewCar.VIN.Trim().ToUpper();
            NewCar.Description = NewCar.Description?.Trim() ?? "";
            NewCar.CreatedAt = DateTime.Now;

            /*
                IMPORTANT FEATURE:
                License Plate and VIN duplicate checking prevents duplicate vehicle identity records.
            */

            var duplicateError = await CheckDuplicateVehicleIdentifiersAsync(
                NewCar.LicensePlate,
                NewCar.VIN,
                null
            );

            if (!string.IsNullOrWhiteSpace(duplicateError))
            {
                TempData["Error"] = duplicateError;
                return RedirectToPage();
            }

            // Save uploaded image if admin selected one.
            var imageResult = await SaveCarImageAsync(CarImage);

            if (!imageResult.IsSuccess)
            {
                TempData["Error"] = imageResult.Message;
                return RedirectToPage();
            }

            NewCar.ImageUrl = imageResult.FileUrl ?? "";

            try
            {
                // Add new car to database.
                _context.Cars.Add(NewCar);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Car added successfully.";
                return RedirectToPage();
            }
            catch
            {
                // Delete uploaded image if database saving fails.
                DeleteImageIfExists(NewCar.ImageUrl);

                TempData["Error"] = "Something went wrong while adding the car. Please try again.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostEditAsync()
        {
            // Find selected car.
            var car = await _context.Cars.FindAsync(EditCarId);

            if (car == null)
            {
                TempData["Error"] = "Car not found.";
                return RedirectToPage();
            }

            /*
                IMPORTANT FEATURE:
                Edit Car Info uses a separate modal with pre-filled values.
                Status is intentionally excluded because status has its own quick update form.
            */

            var validationError = ValidateCarInformationInput(
                EditMake,
                EditModel,
                EditYear,
                EditColor,
                EditCategory,
                EditPricePerDay,
                EditSeats,
                EditTransmission,
                EditLicensePlate,
                EditVIN
            );

            if (!string.IsNullOrWhiteSpace(validationError))
            {
                TempData["Error"] = validationError;
                return RedirectToPage();
            }

            // Normalize license plate and VIN.
            var normalizedLicensePlate = EditLicensePlate.Trim().ToUpper();
            var normalizedVin = EditVIN.Trim().ToUpper();

            // Check if plate/VIN is already used by another car.
            var duplicateError = await CheckDuplicateVehicleIdentifiersAsync(
                normalizedLicensePlate,
                normalizedVin,
                EditCarId
            );

            if (!string.IsNullOrWhiteSpace(duplicateError))
            {
                TempData["Error"] = duplicateError;
                return RedirectToPage();
            }

            // Keep old image path so it can be deleted after successful update.
            var oldImageUrl = car.ImageUrl;
            string? newImageUrl = null;

            // Save replacement image only if admin uploaded a new one.
            if (EditCarImage != null && EditCarImage.Length > 0)
            {
                var imageResult = await SaveCarImageAsync(EditCarImage);

                if (!imageResult.IsSuccess)
                {
                    TempData["Error"] = imageResult.Message;
                    return RedirectToPage();
                }

                newImageUrl = imageResult.FileUrl;
            }

            try
            {
                // Update editable car information only.
                car.Make = EditMake.Trim();
                car.Model = EditModel.Trim();
                car.Year = EditYear;
                car.Color = EditColor.Trim();
                car.Category = EditCategory.Trim();
                car.PricePerDay = EditPricePerDay;
                car.Seats = EditSeats;
                car.Transmission = EditTransmission.Trim();
                car.LicensePlate = normalizedLicensePlate;
                car.VIN = normalizedVin;
                car.Description = EditDescription?.Trim() ?? "";

                // Replace image only if a new image was uploaded.
                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    car.ImageUrl = newImageUrl;
                }

                await _context.SaveChangesAsync();

                // Delete old image only after database update succeeds.
                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    DeleteImageIfExists(oldImageUrl);
                }

                TempData["Success"] = "Car information updated successfully.";
                return RedirectToPage();
            }
            catch
            {
                // Delete new image if database update fails.
                if (!string.IsNullOrWhiteSpace(newImageUrl))
                {
                    DeleteImageIfExists(newImageUrl);
                }

                TempData["Error"] = "Something went wrong while updating the car. Please try again.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            // Find selected car.
            var car = await _context.Cars.FindAsync(CarId);

            if (car == null)
            {
                TempData["Error"] = "Car not found.";
                return RedirectToPage();
            }

            // Validate selected status.
            var statusError = ValidateStatus(Status);

            if (!string.IsNullOrWhiteSpace(statusError))
            {
                TempData["Error"] = statusError;
                return RedirectToPage();
            }

            try
            {
                /*
                    IMPORTANT FEATURE:
                    Quick Status Update controls car availability separately from car information editing.
                */

                car.Status = Status.ToLower().Trim();
                await _context.SaveChangesAsync();

                TempData["Success"] = "Car status updated successfully.";
                return RedirectToPage();
            }
            catch
            {
                TempData["Error"] = "Something went wrong while updating the car status.";
                return RedirectToPage();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            // Find selected car.
            var car = await _context.Cars.FindAsync(CarId);

            if (car == null)
            {
                TempData["Error"] = "Car not found.";
                return RedirectToPage();
            }

            /*
                IMPORTANT FEATURE:
                Cars with booking records are not deleted.
                This preserves booking history, payment records, reports, and agreement references.
            */

            var hasBookings = await _context.Bookings.AnyAsync(b => b.CarId == CarId);

            if (hasBookings)
            {
                TempData["Error"] = "This car has booking records. Set it to inactive instead of deleting.";
                return RedirectToPage();
            }

            var imageUrl = car.ImageUrl;

            try
            {
                _context.Cars.Remove(car);
                await _context.SaveChangesAsync();

                // Delete image after database deletion succeeds.
                DeleteImageIfExists(imageUrl);

                TempData["Success"] = "Car deleted successfully.";
                return RedirectToPage();
            }
            catch
            {
                TempData["Error"] = "Something went wrong while deleting the car. Please try again.";
                return RedirectToPage();
            }
        }

        private string? ValidateCarInformationInput(
            string make,
            string model,
            int year,
            string color,
            string category,
            double pricePerDay,
            int seats,
            string transmission,
            string licensePlate,
            string vin)
        {
            // Validate required text fields.
            if (string.IsNullOrWhiteSpace(make) ||
                string.IsNullOrWhiteSpace(model) ||
                string.IsNullOrWhiteSpace(color) ||
                string.IsNullOrWhiteSpace(category) ||
                string.IsNullOrWhiteSpace(transmission) ||
                string.IsNullOrWhiteSpace(licensePlate) ||
                string.IsNullOrWhiteSpace(vin))
            {
                return "Please fill in all required car information fields.";
            }

            // Validate year range.
            if (year < 1990 || year > DateTime.Now.Year + 1)
            {
                return "Please enter a valid car year.";
            }

            // Validate price.
            if (pricePerDay <= 0)
            {
                return "Price per day must be greater than 0.";
            }

            // Validate seat count.
            if (seats <= 0)
            {
                return "Seats must be greater than 0.";
            }

            // Validate transmission options.
            var allowedTransmissions = new[] { "Automatic", "Manual" };

            if (!allowedTransmissions.Contains(transmission.Trim()))
            {
                return "Invalid transmission type.";
            }

            return null;
        }

        private string? ValidateStatus(string status)
        {
            // Allowed car status values.
            var allowedStatuses = new[] { "available", "booked", "maintenance", "inactive" };

            // Normalize input status.
            var normalizedStatus = status?.ToLower().Trim() ?? "";

            // Validate status.
            if (!allowedStatuses.Contains(normalizedStatus))
            {
                return "Invalid car status.";
            }

            return null;
        }

        private async Task<string?> CheckDuplicateVehicleIdentifiersAsync(
            string licensePlate,
            string vin,
            int? currentCarId)
        {
            // Check duplicate license plate, excluding current edited car if applicable.
            var duplicatePlate = await _context.Cars.AnyAsync(c =>
                c.LicensePlate == licensePlate &&
                (!currentCarId.HasValue || c.CarId != currentCarId.Value));

            if (duplicatePlate)
            {
                return "License plate already exists in the system.";
            }

            // Check duplicate VIN, excluding current edited car if applicable.
            var duplicateVin = await _context.Cars.AnyAsync(c =>
                c.VIN == vin &&
                (!currentCarId.HasValue || c.CarId != currentCarId.Value));

            if (duplicateVin)
            {
                return "VIN already exists in the system.";
            }

            return null;
        }

        private async Task<(bool IsSuccess, string? FileUrl, string Message)> SaveCarImageAsync(IFormFile? image)
        {
            // If no image is uploaded, continue without changing image.
            if (image == null || image.Length == 0)
            {
                return (true, null, "");
            }

            /*
                IMPORTANT FEATURE:
                Image validation blocks invalid file types and oversized files before saving.
            */

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                return (false, null, "Invalid image format. Please upload JPG, PNG, or WEBP.");
            }

            var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };

            if (!allowedContentTypes.Contains(image.ContentType.ToLower()))
            {
                return (false, null, "Invalid image content type.");
            }

            var maxFileSize = 50 * 1024 * 1024;

            if (image.Length > maxFileSize)
            {
                return (false, null, "Image size is too large. Maximum allowed size is 50 MB.");
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
                await image.CopyToAsync(stream);
            }

            return (true, $"/images/cars/{fileName}", "");
        }

        private void DeleteImageIfExists(string? imageUrl)
        {
            // Stop if there is no image path.
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            // Convert public image URL to physical file path.
            var imagePath = imageUrl.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString());
            var fullImagePath = Path.Combine(_environment.WebRootPath, imagePath);

            // Delete only if file exists.
            if (System.IO.File.Exists(fullImagePath))
            {
                System.IO.File.Delete(fullImagePath);
            }
        }

        private async Task LoadCarPageDataAsync()
        {
            // Start query from Cars table.
            var query = _context.Cars.AsQueryable();

            // Apply search filter if admin entered a keyword.
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

            // Load filtered cars.
            Cars = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // Count all cars.
            TotalCars = await _context.Cars.CountAsync();

            // Count available cars.
            AvailableCars = await _context.Cars
                .CountAsync(c => c.Status == "available");

            // Compute unavailable cars.
            NotAvailableCars = TotalCars - AvailableCars;

            if (TotalCars == 0)
            {
                // Avoid division by zero.
                AvailablePercent = 0;
                NotAvailablePercent = 0;
            }
            else
            {
                // Compute available percentage for donut chart.
                AvailablePercent = (int)Math.Round((double)AvailableCars / TotalCars * 100);

                // Remaining percentage is not available.
                NotAvailablePercent = 100 - AvailablePercent;
            }
        }
    }
}