using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Admin.Bookings
{
    public class BookingListModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BookingListModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public List<AdminBookingListItem> Bookings { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "live";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public int BookingId { get; set; }

        [BindProperty]
        public IFormFile? SignedAgreementFile { get; set; }

        public int LiveCount { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public double TotalIncome { get; set; }

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostUploadAgreementAsync()
        {
            // Admin must select a signed agreement file before uploading.
            if (SignedAgreementFile == null || SignedAgreementFile.Length == 0)
            {
                TempData["Error"] = "Please select a signed agreement file before uploading.";
                return RedirectToPage(new { tab = "pending" });
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(SignedAgreementFile.FileName).ToLower();

            // Only PDF and image files are allowed.
            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Invalid file type. Only PDF, JPG, JPEG, and PNG files are allowed.";
                return RedirectToPage(new { tab = "pending" });
            }

            var maxFileSize = 5 * 1024 * 1024;

            // Limit uploaded file to 5 MB.
            if (SignedAgreementFile.Length > maxFileSize)
            {
                TempData["Error"] = "File size is too large. Maximum allowed size is 5 MB.";
                return RedirectToPage(new { tab = "pending" });
            }

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            var agreement = await _context.RentalAgreements.FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found for this booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            try
            {
                // Create upload folder if it does not exist.
                var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "rental-agreements", "signed");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                // Use a unique file name to avoid overwriting uploaded agreements.
                var fileName = $"signed_agreement_booking_{BookingId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await SignedAgreementFile.CopyToAsync(stream);
                }

                // Save public file path to database.
                agreement.SignedAgreementFileUrl = $"/uploads/rental-agreements/signed/{fileName}";
                agreement.SignedUploadedAt = DateTime.Now;
                agreement.Status = "signed_uploaded";

                await _context.SaveChangesAsync();

                TempData["Success"] = "Signed rental agreement uploaded successfully.";
                return RedirectToPage(new { tab = "pending" });
            }
            catch
            {
                TempData["Error"] = "Something went wrong while uploading the signed agreement.";
                return RedirectToPage(new { tab = "pending" });
            }
        }

        public async Task<IActionResult> OnPostApproveBookingAsync()
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            var agreement = await _context.RentalAgreements.FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Booking cannot be approved unless admin uploaded the signed agreement.
            if (string.IsNullOrWhiteSpace(agreement.SignedAgreementFileUrl))
            {
                TempData["Error"] = "Please upload the signed rental agreement before approving this booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            booking.Status = "approved";
            agreement.Status = "approved";
            agreement.ApprovedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking approved successfully. It is now moved to Live Bookings.";
            return RedirectToPage(new { tab = "live" });
        }

        public async Task<IActionResult> OnPostCancelBookingAsync()
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (booking.Status == "completed")
            {
                TempData["Error"] = "Completed bookings cannot be cancelled.";
                return RedirectToPage(new { tab = "history" });
            }

            var agreement = await _context.RentalAgreements.FirstOrDefaultAsync(a => a.BookingId == BookingId);

            booking.Status = "cancelled";

            // Cancel related agreement if it exists.
            if (agreement != null)
            {
                agreement.Status = "cancelled";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking cancelled successfully.";
            return RedirectToPage(new { tab = "history" });
        }

        public async Task<IActionResult> OnPostReturnBookingAsync()
        {
            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "live" });
            }

            // Only approved bookings can be marked as returned/completed.
            if (booking.Status != "approved")
            {
                TempData["Error"] = "Only approved live bookings can be marked as returned.";
                return RedirectToPage(new { tab = "live" });
            }

            booking.Status = "completed";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Vehicle returned successfully. Booking is now completed.";
            return RedirectToPage(new { tab = "history" });
        }

        private async Task LoadPageDataAsync()
        {
            var allBookings = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var agreements = await _context.RentalAgreements.ToListAsync();
            var payments = await _context.Payments.ToListAsync();

            LiveCount = allBookings.Count(b => b.Status == "approved");
            PendingCount = allBookings.Count(b => b.Status == "pending");
            CompletedCount = allBookings.Count(b => b.Status == "completed");
            CancelledCount = allBookings.Count(b => b.Status == "cancelled");

            TotalIncome = payments
                .Where(p => p.PaymentStatus == "paid")
                .Sum(p => p.Amount);

            var selectedTab = string.IsNullOrWhiteSpace(Tab) ? "live" : Tab.ToLower();

            IEnumerable<Booking> filteredBookings = selectedTab switch
            {
                "pending" => allBookings.Where(b => b.Status == "pending"),
                "history" => allBookings.Where(b => b.Status == "completed" || b.Status == "cancelled"),
                _ => allBookings.Where(b => b.Status == "approved")
            };

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.ToLower();

                filteredBookings = filteredBookings.Where(b =>
                    b.BookingId.ToString().Contains(keyword) ||
                    (b.User != null && $"{b.User.FirstName} {b.User.LastName}".ToLower().Contains(keyword)) ||
                    (b.Car != null && $"{b.Car.Make} {b.Car.Model}".ToLower().Contains(keyword))
                );
            }

            Bookings = filteredBookings.Select(b =>
            {
                var agreement = agreements.FirstOrDefault(a => a.BookingId == b.BookingId);
                var payment = payments.FirstOrDefault(p => p.BookingId == b.BookingId);

                return new AdminBookingListItem
                {
                    BookingId = b.BookingId,
                    CarNo = b.CarId.ToString("D4"),
                    CarModel = b.Car == null ? "Unknown Car" : $"{b.Car.Make} {b.Car.Model}",
                    CustomerName = b.User == null ? "Unknown User" : $"{b.User.FirstName} {b.User.LastName}",
                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    TotalPrice = b.TotalPrice,
                    BookingStatus = b.Status,
                    PaymentStatus = payment?.PaymentStatus ?? "No Payment",
                    PaymentMethod = payment?.PaymentMethod ?? "N/A",
                    AgreementStatus = agreement?.Status ?? "No Agreement",
                    GeneratedAgreementFileUrl = agreement?.GeneratedAgreementFileUrl,
                    SignedAgreementFileUrl = agreement?.SignedAgreementFileUrl,
                    CreatedAt = b.CreatedAt
                };
            }).ToList();
        }
    }

    public class AdminBookingListItem
    {
        public int BookingId { get; set; }

        public string CarNo { get; set; } = "";

        public string CarModel { get; set; } = "";

        public string CustomerName { get; set; } = "";

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public double TotalPrice { get; set; }

        public string BookingStatus { get; set; } = "";

        public string PaymentStatus { get; set; } = "";

        public string PaymentMethod { get; set; } = "";

        public string AgreementStatus { get; set; } = "";

        public string? GeneratedAgreementFileUrl { get; set; }

        public string? SignedAgreementFileUrl { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}