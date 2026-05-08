using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Bookings
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<BookingCardViewModel> LiveBookings { get; set; } = new();
        public List<BookingCardViewModel> DisplayedBookings { get; set; } = new();

        public int AllCount { get; set; }
        public int PendingCount { get; set; }
        public int UpcomingCount { get; set; }
        public int CancelledCount { get; set; }
        public int HistoryCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "all";

        [BindProperty]
        public int BookingId { get; set; }

        [BindProperty]
        public int Rating { get; set; }

        [BindProperty]
        public string? Comment { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadBookingsAsync(userId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostCancelBookingAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b =>
                    b.BookingId == BookingId &&
                    b.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { Tab });
            }

            var status = NormalizeStatus(booking.Status);

            /*
                IMPORTANT FEATURE:
                Customer can cancel ONLY pending bookings.
                Approved, live, upcoming, completed, and history bookings cannot be cancelled by the customer.
            */
            if (status != "pending")
            {
                TempData["Error"] = "Only pending bookings can be cancelled.";
                return RedirectToPage(new { Tab });
            }

            try
            {
                booking.Status = "cancelled";

                await _context.SaveChangesAsync();

                TempData["Success"] = "Booking cancelled successfully.";
                return RedirectToPage(new { Tab = "cancelled" });
            }
            catch
            {
                TempData["Error"] = "Something went wrong while cancelling the booking.";
                return RedirectToPage(new { Tab });
            }
        }

        public async Task<IActionResult> OnPostSubmitReviewAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            if (Rating < 1 || Rating > 5)
            {
                TempData["Error"] = "Please select a rating from 1 to 5.";
                return RedirectToPage(new { Tab = "history" });
            }

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b =>
                    b.BookingId == BookingId &&
                    b.UserId == userId.Value);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { Tab = "history" });
            }

            var now = DateTime.Now;
            var status = NormalizeStatus(booking.Status);

            var isHistory =
                status == "completed" ||
                (status == "approved" && booking.EndDate <= now);

            if (!isHistory)
            {
                TempData["Error"] = "You can only review completed booking history.";
                return RedirectToPage(new { Tab = "history" });
            }

            var alreadyReviewed = await _context.Reviews
                .AnyAsync(r =>
                    r.BookingId == booking.BookingId &&
                    r.UserId == userId.Value);

            if (alreadyReviewed)
            {
                TempData["Error"] = "You already reviewed this booking.";
                return RedirectToPage(new { Tab = "history" });
            }

            try
            {
                var review = new Review
                {
                    UserId = userId.Value,
                    CarId = booking.CarId,
                    BookingId = booking.BookingId,
                    Rating = Rating,
                    Comment = Comment,
                    CreatedAt = DateTime.Now
                };

                _context.Reviews.Add(review);

                if (status == "approved" && booking.EndDate <= now)
                {
                    booking.Status = "completed";
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = "Review submitted successfully.";
                return RedirectToPage(new { Tab = "history" });
            }
            catch
            {
                TempData["Error"] = "Something went wrong while submitting your review.";
                return RedirectToPage(new { Tab = "history" });
            }
        }

        private async Task LoadBookingsAsync(int userId)
        {
            var now = DateTime.Now;

            var bookings = await _context.Bookings
                .Include(b => b.Car)
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var reviews = await _context.Reviews
                .Where(r => r.UserId == userId)
                .ToListAsync();

            var rentalAgreements = await _context.RentalAgreements
                .Where(a => bookings.Select(b => b.BookingId).Contains(a.BookingId))
                .ToListAsync();

            var bookingRows = bookings
                .Select(booking => MapBookingRow(booking, reviews, rentalAgreements, now))
                .ToList();

            LiveBookings = bookingRows
                .Where(b => b.IsLive)
                .OrderBy(b => b.EndDate)
                .ToList();

            var pendingBookings = bookingRows
                .Where(b => b.BookingStatus == "pending")
                .OrderBy(b => b.StartDate)
                .ToList();

            var upcomingBookings = bookingRows
                .Where(b => b.IsUpcoming)
                .OrderBy(b => b.StartDate)
                .ToList();

            var cancelledBookings = bookingRows
                .Where(b => b.BookingStatus == "cancelled")
                .OrderByDescending(b => b.StartDate)
                .ToList();

            var historyBookings = bookingRows
                .Where(b => b.IsHistory)
                .OrderByDescending(b => b.EndDate)
                .ToList();

            AllCount = bookingRows.Count(b => !b.IsLive);
            PendingCount = pendingBookings.Count;
            UpcomingCount = upcomingBookings.Count;
            CancelledCount = cancelledBookings.Count;
            HistoryCount = historyBookings.Count;

            Tab = NormalizeTab(Tab);

            DisplayedBookings = Tab switch
            {
                "pending" => pendingBookings,
                "upcoming" => upcomingBookings,
                "cancelled" => cancelledBookings,
                "history" => historyBookings,
                _ => bookingRows
                    .Where(b => !b.IsLive)
                    .OrderByDescending(b => b.CreatedAt)
                    .ToList()
            };
        }

        private BookingCardViewModel MapBookingRow(
            Booking booking,
            List<Review> reviews,
            List<RentalAgreement> rentalAgreements,
            DateTime now)
        {
            var status = NormalizeStatus(booking.Status);

            var isLive =
                status == "approved" &&
                booking.StartDate <= now &&
                booking.EndDate > now;

            var isUpcoming =
                status == "approved" &&
                booking.StartDate > now;

            var isHistory =
                status == "completed" ||
                (status == "approved" && booking.EndDate <= now);

            /*
                IMPORTANT FEATURE:
                Only pending bookings can be cancelled by the customer.
            */
            var canCancel = status == "pending";

            var review = reviews.FirstOrDefault(r => r.BookingId == booking.BookingId);
            var agreement = rentalAgreements.FirstOrDefault(a => a.BookingId == booking.BookingId);

            var carName = booking.Car == null
                ? "Unknown Car"
                : $"{booking.Car.Make} {booking.Car.Model}";

            return new BookingCardViewModel
            {
                BookingId = booking.BookingId,
                CarId = booking.CarId,
                CarName = carName,
                Category = booking.Car?.Category ?? "",
                ImageUrl = booking.Car?.ImageUrl ?? "",
                Seats = booking.Car?.Seats ?? 0,
                Transmission = booking.Car?.Transmission ?? "",
                Color = booking.Car?.Color ?? "",
                PricePerDay = booking.Car?.PricePerDay ?? 0,

                StartDate = booking.StartDate,
                EndDate = booking.EndDate,
                CreatedAt = booking.CreatedAt,
                EndDateIso = booking.EndDate.ToString("yyyy-MM-ddTHH:mm:ss"),

                TotalPrice = booking.TotalPrice,
                BookingStatus = status,
                DisplayStatus = GetDisplayStatus(status, isLive, isUpcoming, isHistory),

                IsLive = isLive,
                IsUpcoming = isUpcoming,
                IsHistory = isHistory,
                CanCancel = canCancel,

                HasReview = review != null,
                ExistingRating = review?.Rating,
                ExistingComment = review?.Comment,
                CanReview = isHistory && review == null,

                RentalAgreementStatus = agreement?.Status ?? "No agreement record",
                GeneratedAgreementFileUrl = agreement?.GeneratedAgreementFileUrl,
                SignedAgreementFileUrl = agreement?.SignedAgreementFileUrl,
                HasSignedAgreement = !string.IsNullOrWhiteSpace(agreement?.SignedAgreementFileUrl)
            };
        }

        private string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? "pending"
                : status.ToLower().Trim();
        }

        private string NormalizeTab(string? tab)
        {
            var value = string.IsNullOrWhiteSpace(tab)
                ? "all"
                : tab.ToLower().Trim();

            var allowedTabs = new[]
            {
                "all",
                "pending",
                "upcoming",
                "cancelled",
                "history"
            };

            return allowedTabs.Contains(value) ? value : "all";
        }

        private string GetDisplayStatus(string status, bool isLive, bool isUpcoming, bool isHistory)
        {
            if (isLive)
            {
                return "Live Booking";
            }

            if (isUpcoming)
            {
                return "Upcoming";
            }

            if (isHistory)
            {
                return "History";
            }

            return status switch
            {
                "pending" => "Pending",
                "approved" => "Approved",
                "cancelled" => "Cancelled",
                "completed" => "Completed",
                _ => status
            };
        }
    }

    public class BookingCardViewModel
    {
        public int BookingId { get; set; }
        public int CarId { get; set; }

        public string CarName { get; set; } = "";
        public string Category { get; set; } = "";
        public string ImageUrl { get; set; } = "";

        public int Seats { get; set; }
        public string Transmission { get; set; } = "";
        public string Color { get; set; } = "";

        public double PricePerDay { get; set; }
        public double TotalPrice { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public string EndDateIso { get; set; } = "";

        public string BookingStatus { get; set; } = "";
        public string DisplayStatus { get; set; } = "";

        public bool IsLive { get; set; }
        public bool IsUpcoming { get; set; }
        public bool IsHistory { get; set; }

        public bool CanCancel { get; set; }
        public bool CanReview { get; set; }

        public bool HasReview { get; set; }
        public int? ExistingRating { get; set; }
        public string? ExistingComment { get; set; }

        public string RentalAgreementStatus { get; set; } = "";
        public string? GeneratedAgreementFileUrl { get; set; }
        public string? SignedAgreementFileUrl { get; set; }
        public bool HasSignedAgreement { get; set; }
    }
}