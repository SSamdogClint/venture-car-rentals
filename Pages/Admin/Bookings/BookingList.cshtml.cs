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

        /*
            IMPORTANT:
            Penalty rule:
            - Under 45 minutes late = no penalty.
            - 45 minutes or more late = ₱200 per started hour.
        */
        private const double PenaltyPerHour = 200;
        private const int PenaltyGraceMinutes = 45;

        public BookingListModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public List<AdminBookingListItem> Bookings { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "live";

        /*
            IMPORTANT:
            ViewMode controls the right section.
            bookings = normal tabs/table
            stats = graph/statistics panel
        */
        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "bookings";

        /*
            StatType controls which stat card graph is opened.
            income = income graph
            status = booking status graph
        */
        [BindProperty(SupportsGet = true)]
        public string StatType { get; set; } = "status";

        /*
            StatRange controls graph filter.
            today, weekly, monthly, overall
        */
        [BindProperty(SupportsGet = true)]
        public string StatRange { get; set; } = "today";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public int BookingId { get; set; }

        [BindProperty]
        public IFormFile? SignedAgreementFile { get; set; }

        public int LiveCount { get; set; }
        public int UpcomingCount { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public int OverdueCount { get; set; }
        public int UnpaidPenaltyCount { get; set; }

        public double TodayIncome { get; set; }

        /*
            These strings are used by Chart.js in the Razor page.
        */
        public string ChartLabelsJson { get; set; } = "[]";
        public string IncomeChartDataJson { get; set; } = "[]";
        public string LiveChartDataJson { get; set; } = "[]";
        public string UpcomingChartDataJson { get; set; } = "[]";
        public string OverdueChartDataJson { get; set; } = "[]";
        public string UnpaidPenaltyChartDataJson { get; set; } = "[]";
        public string CompletedChartDataJson { get; set; } = "[]";
        public string CancelledChartDataJson { get; set; } = "[]";

        public async Task OnGetAsync()
        {
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostUploadAgreementAsync()
        {
            if (SignedAgreementFile == null || SignedAgreementFile.Length == 0)
            {
                TempData["Error"] = "Please select a signed agreement file before uploading.";
                return RedirectToPage(new { tab = "pending" });
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(SignedAgreementFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Invalid file type. Only PDF, JPG, JPEG, and PNG files are allowed.";
                return RedirectToPage(new { tab = "pending" });
            }

            var maxFileSize = 5 * 1024 * 1024;

            if (SignedAgreementFile.Length > maxFileSize)
            {
                TempData["Error"] = "File size is too large. Maximum allowed size is 5 MB.";
                return RedirectToPage(new { tab = "pending" });
            }

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (!SameStatus(booking.Status, "pending"))
            {
                TempData["Error"] = "Only pending bookings can upload a signed agreement.";
                return RedirectToPage(new { tab = "pending" });
            }

            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found for this booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            try
            {
                var webRootPath = _environment.WebRootPath;

                if (string.IsNullOrWhiteSpace(webRootPath))
                {
                    TempData["Error"] = "Web root path is not available. Please check your wwwroot folder.";
                    return RedirectToPage(new { tab = "pending" });
                }

                var uploadFolder = Path.Combine(webRootPath, "uploads", "rental-agreements", "signed");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                var fileName = $"signed_agreement_booking_{BookingId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await SignedAgreementFile.CopyToAsync(stream);
                }

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
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (!SameStatus(booking.Status, "pending"))
            {
                TempData["Error"] = "Only pending bookings can be approved.";
                return RedirectToPage(new { tab = "pending" });
            }

            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (string.IsNullOrWhiteSpace(agreement.SignedAgreementFileUrl))
            {
                TempData["Error"] = "Please upload the signed rental agreement before approving this booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (booking.Car == null)
            {
                TempData["Error"] = "Car record not found. Booking cannot be approved.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (SameStatus(booking.Car.Status, "maintenance") || SameStatus(booking.Car.Status, "inactive"))
            {
                TempData["Error"] = "This car is currently not available for booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            var hasOverlappingBooking = await _context.Bookings.AnyAsync(b =>
                b.BookingId != booking.BookingId &&
                b.CarId == booking.CarId &&
                (b.Status == "approved" || b.Status == "started") &&
                b.StartDate < booking.EndDate &&
                b.EndDate > booking.StartDate
            );

            if (hasOverlappingBooking)
            {
                TempData["Error"] = "This car already has an approved or live booking within the selected date range.";
                return RedirectToPage(new { tab = "pending" });
            }

            booking.Status = "approved";

            agreement.Status = "approved";
            agreement.ApprovedAt = DateTime.Now;

            booking.Car.Status = "booked";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking approved successfully. It is now in Upcoming Bookings.";
            return RedirectToPage(new { tab = "upcoming" });
        }

        public async Task<IActionResult> OnPostStartBookingAsync()
        {
            var now = DateTime.Now;

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            if (!SameStatus(booking.Status, "approved"))
            {
                TempData["Error"] = "Only upcoming approved bookings can be started.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            if (booking.EndDate <= now)
            {
                TempData["Error"] = "This booking cannot be started because the return time has already passed.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            if (booking.Car == null)
            {
                TempData["Error"] = "Car record not found. Booking cannot be started.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            var hasAnotherLiveBooking = await _context.Bookings.AnyAsync(b =>
                b.BookingId != booking.BookingId &&
                b.CarId == booking.CarId &&
                b.Status == "started"
            );

            if (hasAnotherLiveBooking)
            {
                TempData["Error"] = "This car already has a live booking.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            booking.Status = "started";
            booking.StartDate = now;
            booking.Car.Status = "booked";

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking started successfully. It is now shown as a Live Booking.";
            return RedirectToPage(new { tab = "live" });
        }

        public async Task<IActionResult> OnPostCancelBookingAsync()
        {
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            if (!SameStatus(booking.Status, "pending") && !SameStatus(booking.Status, "approved"))
            {
                TempData["Error"] = "Only pending or upcoming bookings can be cancelled.";
                return RedirectToPage(new { tab = Tab });
            }

            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            booking.Status = "cancelled";

            if (agreement != null)
            {
                agreement.Status = "cancelled";
            }

            if (booking.Car != null &&
                !SameStatus(booking.Car.Status, "maintenance") &&
                !SameStatus(booking.Car.Status, "inactive"))
            {
                booking.Car.Status = "available";
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking cancelled successfully.";
            return RedirectToPage(new { tab = "history" });
        }

        public async Task<IActionResult> OnPostReturnBookingAsync()
        {
            var now = DateTime.Now;

            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "live" });
            }

            if (!SameStatus(booking.Status, "started"))
            {
                TempData["Error"] = "Only live bookings can be marked as returned.";
                return RedirectToPage(new { tab = "live" });
            }

            var overdueHours = GetOverdueHours(booking.EndDate, now);
            var penaltyAmount = overdueHours * PenaltyPerHour;

            booking.Status = "completed";

            if (booking.Car != null &&
                !SameStatus(booking.Car.Status, "maintenance") &&
                !SameStatus(booking.Car.Status, "inactive"))
            {
                booking.Car.Status = "available";
            }

            if (penaltyAmount > 0)
            {
                var existingPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);

                if (existingPenalty == null)
                {
                    var penalty = new Penalty
                    {
                        BookingId = booking.BookingId,
                        OverdueHours = overdueHours,
                        RatePerHour = PenaltyPerHour,
                        Amount = penaltyAmount,
                        Status = "unpaid",
                        CreatedAt = DateTime.Now
                    };

                    _context.Penalties.Add(penalty);
                }
                else
                {
                    existingPenalty.OverdueHours = overdueHours;
                    existingPenalty.RatePerHour = PenaltyPerHour;
                    existingPenalty.Amount = penaltyAmount;
                    existingPenalty.Status = "unpaid";
                    existingPenalty.CreatedAt = DateTime.Now;
                }
            }

            await _context.SaveChangesAsync();

            if (penaltyAmount > 0)
            {
                TempData["Success"] = $"Vehicle returned successfully. Booking completed with ₱{penaltyAmount:N2} penalty for {overdueHours} overdue hour(s).";
            }
            else
            {
                TempData["Success"] = "Vehicle returned successfully. Booking is now completed. No penalty was charged because the delay was under 45 minutes.";
            }

            return RedirectToPage(new { tab = "history" });
        }

        public async Task<IActionResult> OnPostMarkPenaltyPaidAsync()
        {
            var penalty = await _context.Penalties
                .FirstOrDefaultAsync(p => p.BookingId == BookingId);

            if (penalty == null)
            {
                TempData["Error"] = "Penalty record not found for this booking.";
                return RedirectToPage(new { tab = "unpaid" });
            }

            if (SameStatus(penalty.Status, "paid"))
            {
                TempData["Error"] = "This penalty is already marked as paid.";
                return RedirectToPage(new { tab = "unpaid" });
            }

            penalty.Status = "paid";
            penalty.PaidAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Penalty payment marked as paid successfully.";
            return RedirectToPage(new { tab = "unpaid" });
        }

        private async Task LoadPageDataAsync()
        {
            var now = DateTime.Now;
            var todayStart = DateTime.Today;
            var tomorrowStart = todayStart.AddDays(1);

            ViewMode = NormalizeViewMode(ViewMode);
            StatType = NormalizeStatType(StatType);
            StatRange = NormalizeStatRange(StatRange);

            var allBookings = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var bookingIds = allBookings
                .Select(b => b.BookingId)
                .ToList();

            var agreements = await _context.RentalAgreements
                .Where(a => bookingIds.Contains(a.BookingId))
                .ToListAsync();

            var payments = await _context.Payments
                .Where(p => bookingIds.Contains(p.BookingId))
                .ToListAsync();

            var penalties = await _context.Penalties
                .Where(p => bookingIds.Contains(p.BookingId))
                .ToListAsync();

            var unpaidPenaltyBookingIds = penalties
                .Where(p => p.Amount > 0 && !SameStatus(p.Status, "paid"))
                .Select(p => p.BookingId)
                .ToHashSet();

            LiveCount = allBookings.Count(b => SameStatus(b.Status, "started"));
            UpcomingCount = allBookings.Count(b => SameStatus(b.Status, "approved"));
            PendingCount = allBookings.Count(b => SameStatus(b.Status, "pending"));
            CompletedCount = allBookings.Count(b => SameStatus(b.Status, "completed"));
            CancelledCount = allBookings.Count(b => SameStatus(b.Status, "cancelled"));
            OverdueCount = allBookings.Count(b => SameStatus(b.Status, "started") && b.EndDate < now);

            UnpaidPenaltyCount = allBookings.Count(b =>
                SameStatus(b.Status, "completed") &&
                unpaidPenaltyBookingIds.Contains(b.BookingId)
            );

            var todayApprovedBookingIds = agreements
                .Where(a =>
                    a.ApprovedAt.HasValue &&
                    a.ApprovedAt.Value >= todayStart &&
                    a.ApprovedAt.Value < tomorrowStart
                )
                .Select(a => a.BookingId)
                .ToHashSet();

            TodayIncome = allBookings
                .Where(b =>
                    todayApprovedBookingIds.Contains(b.BookingId) &&
                    (
                        SameStatus(b.Status, "approved") ||
                        SameStatus(b.Status, "started") ||
                        SameStatus(b.Status, "completed")
                    )
                )
                .Sum(b => b.TotalPrice);

            BuildChartData(allBookings, agreements, penalties, now);

            var selectedTab = NormalizeTab(Tab);
            Tab = selectedTab;

            IEnumerable<Booking> filteredBookings = selectedTab switch
            {
                "pending" => allBookings.Where(b => SameStatus(b.Status, "pending")),

                "upcoming" => allBookings.Where(b => SameStatus(b.Status, "approved")),

                "unpaid" => allBookings.Where(b =>
                    SameStatus(b.Status, "completed") &&
                    unpaidPenaltyBookingIds.Contains(b.BookingId)),

                "history" => allBookings.Where(b =>
                    SameStatus(b.Status, "completed") ||
                    SameStatus(b.Status, "cancelled")),

                _ => allBookings.Where(b => SameStatus(b.Status, "started"))
            };

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim().ToLower();

                filteredBookings = filteredBookings.Where(b =>
                    b.BookingId.ToString().Contains(keyword) ||
                    (b.User != null && $"{b.User.FirstName} {b.User.LastName}".ToLower().Contains(keyword)) ||
                    (b.User != null && !string.IsNullOrWhiteSpace(b.User.Email) && b.User.Email.ToLower().Contains(keyword)) ||
                    (b.Car != null && $"{b.Car.Make} {b.Car.Model}".ToLower().Contains(keyword))
                );
            }

            Bookings = filteredBookings.Select(b =>
            {
                var agreement = agreements.FirstOrDefault(a => a.BookingId == b.BookingId);
                var payment = payments.FirstOrDefault(p => p.BookingId == b.BookingId);
                var savedPenalty = penalties.FirstOrDefault(p => p.BookingId == b.BookingId);

                var liveOverdueHours = GetOverdueHours(b.EndDate, now);
                var livePenaltyAmount = liveOverdueHours * PenaltyPerHour;

                var isCompleted = SameStatus(b.Status, "completed");

                return new AdminBookingListItem
                {
                    BookingId = b.BookingId,
                    CarNo = b.CarId.ToString("D4"),
                    CarModel = b.Car == null ? "Unknown Car" : $"{b.Car.Make} {b.Car.Model}",
                    CustomerName = b.User == null ? "Unknown User" : $"{b.User.FirstName} {b.User.LastName}",
                    CustomerEmail = b.User?.Email ?? "No email",

                    StartDate = b.StartDate,
                    EndDate = b.EndDate,
                    CreatedAt = b.CreatedAt,
                    TotalPrice = b.TotalPrice,

                    BookingStatus = NormalizeStatus(b.Status),
                    DisplayStatus = GetDisplayStatus(b.Status, b.EndDate, now),

                    IsOverdue = SameStatus(b.Status, "started") && b.EndDate < now,

                    OverdueHours = isCompleted
                        ? savedPenalty?.OverdueHours ?? 0
                        : liveOverdueHours,

                    PenaltyAmount = isCompleted
                        ? savedPenalty?.Amount ?? 0
                        : livePenaltyAmount,

                    HasPenalty = isCompleted
                        ? savedPenalty != null && savedPenalty.Amount > 0
                        : livePenaltyAmount > 0,

                    PenaltyStatus = savedPenalty?.Status ?? "No Penalty",
                    PenaltyPaidAt = savedPenalty?.PaidAt,

                    PaymentStatus = payment?.PaymentStatus ?? "No Payment",
                    PaymentMethod = payment?.PaymentMethod ?? "N/A",

                    AgreementStatus = agreement?.Status ?? "No Agreement",
                    GeneratedAgreementFileUrl = agreement?.GeneratedAgreementFileUrl,
                    SignedAgreementFileUrl = agreement?.SignedAgreementFileUrl
                };
            }).ToList();
        }

        /*
            IMPORTANT:
            This builds the graph data shown when the stat card is opened.
        */
        private void BuildChartData(
            List<Booking> allBookings,
            List<RentalAgreement> agreements,
            List<Penalty> penalties,
            DateTime now)
        {
            var buckets = BuildDateBuckets(now, StatRange);

            var labels = buckets.Select(b => b.Label).ToList();

            var incomeData = new List<double>();
            var liveData = new List<int>();
            var upcomingData = new List<int>();
            var overdueData = new List<int>();
            var unpaidPenaltyData = new List<int>();
            var completedData = new List<int>();
            var cancelledData = new List<int>();

            foreach (var bucket in buckets)
            {
                var approvedIds = agreements
                    .Where(a =>
                        a.ApprovedAt.HasValue &&
                        a.ApprovedAt.Value >= bucket.Start &&
                        a.ApprovedAt.Value < bucket.End
                    )
                    .Select(a => a.BookingId)
                    .ToHashSet();

                incomeData.Add(allBookings
                    .Where(b =>
                        approvedIds.Contains(b.BookingId) &&
                        (
                            SameStatus(b.Status, "approved") ||
                            SameStatus(b.Status, "started") ||
                            SameStatus(b.Status, "completed")
                        )
                    )
                    .Sum(b => b.TotalPrice));

                liveData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "started") &&
                    b.StartDate >= bucket.Start &&
                    b.StartDate < bucket.End));

                upcomingData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "approved") &&
                    b.CreatedAt >= bucket.Start &&
                    b.CreatedAt < bucket.End));

                overdueData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "started") &&
                    b.EndDate < now &&
                    b.EndDate >= bucket.Start &&
                    b.EndDate < bucket.End));

                completedData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "completed") &&
                    b.EndDate >= bucket.Start &&
                    b.EndDate < bucket.End));

                cancelledData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "cancelled") &&
                    b.CreatedAt >= bucket.Start &&
                    b.CreatedAt < bucket.End));

                var unpaidPenaltyIds = penalties
                    .Where(p =>
                        p.Amount > 0 &&
                        !SameStatus(p.Status, "paid") &&
                        p.CreatedAt >= bucket.Start &&
                        p.CreatedAt < bucket.End)
                    .Select(p => p.BookingId)
                    .ToHashSet();

                unpaidPenaltyData.Add(unpaidPenaltyIds.Count);
            }

            ChartLabelsJson = ToJson(labels);
            IncomeChartDataJson = ToJson(incomeData);
            LiveChartDataJson = ToJson(liveData);
            UpcomingChartDataJson = ToJson(upcomingData);
            OverdueChartDataJson = ToJson(overdueData);
            UnpaidPenaltyChartDataJson = ToJson(unpaidPenaltyData);
            CompletedChartDataJson = ToJson(completedData);
            CancelledChartDataJson = ToJson(cancelledData);
        }

        private static List<ChartDateBucket> BuildDateBuckets(DateTime now, string range)
        {
            var buckets = new List<ChartDateBucket>();

            if (range == "today")
            {
                var today = DateTime.Today;

                for (var hour = 0; hour < 24; hour += 4)
                {
                    var start = today.AddHours(hour);
                    var end = start.AddHours(4);

                    buckets.Add(new ChartDateBucket
                    {
                        Label = $"{start:htt}",
                        Start = start,
                        End = end
                    });
                }

                return buckets;
            }

            if (range == "weekly")
            {
                var startOfWeek = DateTime.Today.AddDays(-6);

                for (var i = 0; i < 7; i++)
                {
                    var start = startOfWeek.AddDays(i);
                    var end = start.AddDays(1);

                    buckets.Add(new ChartDateBucket
                    {
                        Label = start.ToString("ddd"),
                        Start = start,
                        End = end
                    });
                }

                return buckets;
            }

            if (range == "monthly")
            {
                var startOfMonth = new DateTime(now.Year, now.Month, 1);
                var daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

                for (var day = 1; day <= daysInMonth; day++)
                {
                    var start = new DateTime(now.Year, now.Month, day);
                    var end = start.AddDays(1);

                    buckets.Add(new ChartDateBucket
                    {
                        Label = day.ToString(),
                        Start = start,
                        End = end
                    });
                }

                return buckets;
            }

            /*
                Overall view: last 12 months.
            */
            var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

            for (var i = 0; i < 12; i++)
            {
                var start = firstMonth.AddMonths(i);
                var end = start.AddMonths(1);

                buckets.Add(new ChartDateBucket
                {
                    Label = start.ToString("MMM"),
                    Start = start,
                    End = end
                });
            }

            return buckets;
        }

        private static string ToJson<T>(List<T> values)
        {
            return System.Text.Json.JsonSerializer.Serialize(values);
        }

        private static string NormalizeTab(string? tab)
        {
            return tab?.ToLower().Trim() switch
            {
                "pending" => "pending",
                "upcoming" => "upcoming",
                "history" => "history",
                "unpaid" => "unpaid",
                _ => "live"
            };
        }

        private static string NormalizeViewMode(string? viewMode)
        {
            return viewMode?.ToLower().Trim() == "stats"
                ? "stats"
                : "bookings";
        }

        private static string NormalizeStatType(string? statType)
        {
            return statType?.ToLower().Trim() switch
            {
                "income" => "income",
                _ => "status"
            };
        }

        private static string NormalizeStatRange(string? statRange)
        {
            return statRange?.ToLower().Trim() switch
            {
                "weekly" => "weekly",
                "monthly" => "monthly",
                "overall" => "overall",
                _ => "today"
            };
        }

        private static string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? "pending"
                : status.ToLower().Trim();
        }

        private static bool SameStatus(string? value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetOverdueHours(DateTime endDate, DateTime now)
        {
            if (now <= endDate)
            {
                return 0;
            }

            var overdueTime = now - endDate;

            if (overdueTime.TotalMinutes < PenaltyGraceMinutes)
            {
                return 0;
            }

            return (int)Math.Ceiling(overdueTime.TotalHours);
        }

        private static string GetDisplayStatus(string? status, DateTime endDate, DateTime now)
        {
            var normalizedStatus = NormalizeStatus(status);

            if (normalizedStatus == "started" && endDate < now)
            {
                return "Overdue";
            }

            return normalizedStatus switch
            {
                "pending" => "Pending",
                "approved" => "Upcoming",
                "started" => "Live",
                "completed" => "Completed",
                "cancelled" => "Cancelled",
                _ => normalizedStatus
            };
        }
    }

    public class AdminBookingListItem
    {
        public int BookingId { get; set; }

        public string CarNo { get; set; } = "";
        public string CarModel { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public double TotalPrice { get; set; }

        public string BookingStatus { get; set; } = "";
        public string DisplayStatus { get; set; } = "";

        public bool IsOverdue { get; set; }
        public int OverdueHours { get; set; }
        public double PenaltyAmount { get; set; }

        public bool HasPenalty { get; set; }
        public string PenaltyStatus { get; set; } = "";
        public DateTime? PenaltyPaidAt { get; set; }

        public string PaymentStatus { get; set; } = "";
        public string PaymentMethod { get; set; } = "";

        public string AgreementStatus { get; set; } = "";

        public string? GeneratedAgreementFileUrl { get; set; }
        public string? SignedAgreementFileUrl { get; set; }
    }

    public class ChartDateBucket
    {
        public string Label { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}