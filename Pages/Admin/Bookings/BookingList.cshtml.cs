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
            This is the penalty rule used by the booking system.

            Rule:
            - If the customer is late for less than 45 minutes, no penalty will be charged.
            - If the customer is late for 45 minutes or more, the system charges ₱200 per started hour.

            Example:
            - 30 minutes late = ₱0
            - 45 minutes late = ₱200
            - 1 hour 10 minutes late = ₱400 because it counts as 2 started hours
        */
        private const double PenaltyPerHour = 200;
        private const int PenaltyGraceMinutes = 45;

        public BookingListModel(AppDbContext context, IWebHostEnvironment environment)
        {
            // Database context for accessing Bookings, Cars, Payments, Penalties, Notifications, etc.
            _context = context;

            // Web host environment is used for saving uploaded signed agreement files inside wwwroot.
            _environment = environment;
        }

        // List displayed in the admin booking table.
        public List<AdminBookingListItem> Bookings { get; set; } = new();

        // Current active tab: live, upcoming, pending, history, unpaid.
        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "live";

        /*
            IMPORTANT:
            ViewMode controls what appears on the right section of the admin booking page.

            bookings = normal table and tabs
            stats = statistics graph panel
        */
        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "bookings";

        /*
            IMPORTANT:
            StatType controls which graph is shown.

            income = income graph
            status = booking status comparison graph
        */
        [BindProperty(SupportsGet = true)]
        public string StatType { get; set; } = "status";

        /*
            IMPORTANT:
            StatRange controls the graph date range.

            today = hourly buckets
            weekly = last 7 days
            monthly = current month days
            overall = last 12 months
        */
        [BindProperty(SupportsGet = true)]
        public string StatRange { get; set; } = "today";

        // Search input from admin search bar.
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        // Booking ID used by post actions such as approve, start, return, cancel, mark penalty paid.
        [BindProperty]
        public int BookingId { get; set; }

        // Uploaded signed rental agreement file.
        [BindProperty]
        public IFormFile? SignedAgreementFile { get; set; }

        // Statistic card values.
        public int LiveCount { get; set; }
        public int UpcomingCount { get; set; }
        public int PendingCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
        public int OverdueCount { get; set; }
        public int UnpaidPenaltyCount { get; set; }

        // Today's income from approved bookings.
        public double TodayIncome { get; set; }

        // These JSON strings are passed to Chart.js.
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
            // Loads table data, stat cards, and graph data.
            await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostUploadAgreementAsync()
        {
            // Checks if admin selected a file.
            if (SignedAgreementFile == null || SignedAgreementFile.Length == 0)
            {
                TempData["Error"] = "Please select a signed agreement file before uploading.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Only allow common document/image types for signed agreements.
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(SignedAgreementFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                TempData["Error"] = "Invalid file type. Only PDF, JPG, JPEG, and PNG files are allowed.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Limit upload to 5 MB.
            var maxFileSize = 5 * 1024 * 1024;

            if (SignedAgreementFile.Length > maxFileSize)
            {
                TempData["Error"] = "File size is too large. Maximum allowed size is 5 MB.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Find the booking where the signed agreement belongs.
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Signed agreement upload is only allowed for pending bookings.
            if (!SameStatus(booking.Status, "pending"))
            {
                TempData["Error"] = "Only pending bookings can upload a signed agreement.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Find rental agreement record connected to this booking.
            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found for this booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            try
            {
                // Get wwwroot path.
                var webRootPath = _environment.WebRootPath;

                if (string.IsNullOrWhiteSpace(webRootPath))
                {
                    TempData["Error"] = "Web root path is not available. Please check your wwwroot folder.";
                    return RedirectToPage(new { tab = "pending" });
                }

                // Create upload folder for signed agreements.
                var uploadFolder = Path.Combine(webRootPath, "uploads", "rental-agreements", "signed");

                if (!Directory.Exists(uploadFolder))
                {
                    Directory.CreateDirectory(uploadFolder);
                }

                // Create unique file name to avoid duplicate file name conflict.
                var fileName = $"signed_agreement_booking_{BookingId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadFolder, fileName);

                // Save file into wwwroot/uploads/rental-agreements/signed.
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await SignedAgreementFile.CopyToAsync(stream);
                }

                // Save uploaded file URL into the agreement record.
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
            // Find the selected booking and include the related car.
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Only pending bookings can be approved.
            if (!SameStatus(booking.Status, "pending"))
            {
                TempData["Error"] = "Only pending bookings can be approved.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Find the rental agreement connected to this booking.
            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            if (agreement == null)
            {
                TempData["Error"] = "Rental agreement record not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Admin must upload signed agreement before approval.
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

            // Prevent approval if the car is under maintenance or inactive.
            if (SameStatus(booking.Car.Status, "maintenance") || SameStatus(booking.Car.Status, "inactive"))
            {
                TempData["Error"] = "This car is currently not available for booking.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Checks if the same car already has an approved/live booking within the same schedule.
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

            /*
                IMPORTANT:
                This is where the admin approves the booking.

                Status flow:
                pending  -> approved

                After this, the booking appears under the Upcoming Bookings tab.
                It will not become live yet until admin clicks Start Booking.
            */
            booking.Status = "approved";

            // Update the rental agreement status.
            agreement.Status = "approved";
            agreement.ApprovedAt = DateTime.Now;

            // Mark the car as booked because it already has an approved schedule.
            booking.Car.Status = "booked";

            /*
                IMPORTANT:
                USER NOTIFICATION WHEN BOOKING IS APPROVED

                This creates a notification record for the customer.
                The user notification bell reads from the Notifications table every 10 seconds.
                If this record is not inserted, the user will not receive any notification.

                RecipientType = "user" means this notification belongs to a customer.
                UserId = booking.UserId means only this customer can see it.
            */
            _context.Notifications.Add(new Notification
            {
                RecipientType = "user",
                UserId = booking.UserId,
                Title = "Booking Approved",
                Message = $"Your booking #{booking.BookingId} has been approved by the admin.",
                Type = "booking",
                TargetUrl = "/User/Bookings/Index?Tab=upcoming",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            // Saves booking update, car update, agreement update, and notification insert.
            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking approved successfully. It is now in Upcoming Bookings.";
            return RedirectToPage(new { tab = "upcoming" });
        }

        public async Task<IActionResult> OnPostStartBookingAsync()
        {
            var now = DateTime.Now;

            // Find the approved booking and include the car record.
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            // Only approved bookings can be started.
            if (!SameStatus(booking.Status, "approved"))
            {
                TempData["Error"] = "Only upcoming approved bookings can be started.";
                return RedirectToPage(new { tab = "upcoming" });
            }

            // Prevent starting a booking if its return time already passed.
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

            // Prevent starting if the same car already has another live booking.
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

            /*
                IMPORTANT:
                Admin manually starts the booking.

                Status flow:
                approved -> started

                This is why the live booking will not start automatically.
                It only starts when admin clicks the Start Booking button.
            */
            booking.Status = "started";

            // Save the actual start time as the time admin clicked Start Booking.
            booking.StartDate = now;

            // Keep the car as booked while the customer is using it.
            booking.Car.Status = "booked";

            /*
                IMPORTANT:
                USER NOTIFICATION WHEN BOOKING STARTS

                This tells the customer that the booking is now live.
            */
            _context.Notifications.Add(new Notification
            {
                RecipientType = "user",
                UserId = booking.UserId,
                Title = "Booking Started",
                Message = $"Your booking #{booking.BookingId} is now live.",
                Type = "booking",
                TargetUrl = "/User/Bookings/Index",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking started successfully. It is now shown as a Live Booking.";
            return RedirectToPage(new { tab = "live" });
        }

        public async Task<IActionResult> OnPostCancelBookingAsync()
        {
            // Find selected booking and include car.
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "pending" });
            }

            // Admin can cancel only pending or upcoming bookings.
            if (!SameStatus(booking.Status, "pending") && !SameStatus(booking.Status, "approved"))
            {
                TempData["Error"] = "Only pending or upcoming bookings can be cancelled.";
                return RedirectToPage(new { tab = Tab });
            }

            // Find agreement record if it exists.
            var agreement = await _context.RentalAgreements
                .FirstOrDefaultAsync(a => a.BookingId == BookingId);

            /*
                IMPORTANT:
                Admin cancels the booking.

                Status flow:
                pending/approved -> cancelled
            */
            booking.Status = "cancelled";

            // Cancel rental agreement too.
            if (agreement != null)
            {
                agreement.Status = "cancelled";
            }

            // Return car to available if it is not inactive or under maintenance.
            if (booking.Car != null &&
                !SameStatus(booking.Car.Status, "maintenance") &&
                !SameStatus(booking.Car.Status, "inactive"))
            {
                booking.Car.Status = "available";
            }

            /*
                IMPORTANT:
                USER NOTIFICATION WHEN BOOKING IS CANCELLED

                This tells the customer that admin cancelled the booking.
            */
            _context.Notifications.Add(new Notification
            {
                RecipientType = "user",
                UserId = booking.UserId,
                Title = "Booking Cancelled",
                Message = $"Your booking #{booking.BookingId} has been cancelled by the admin.",
                Type = "booking",
                TargetUrl = "/User/Bookings/Index?Tab=cancelled",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking cancelled successfully.";
            return RedirectToPage(new { tab = "history" });
        }

        public async Task<IActionResult> OnPostReturnBookingAsync()
        {
            var now = DateTime.Now;

            // Find live booking and include car.
            var booking = await _context.Bookings
                .Include(b => b.Car)
                .FirstOrDefaultAsync(b => b.BookingId == BookingId);

            if (booking == null)
            {
                TempData["Error"] = "Booking not found.";
                return RedirectToPage(new { tab = "live" });
            }

            // Only live bookings can be marked as returned.
            if (!SameStatus(booking.Status, "started"))
            {
                TempData["Error"] = "Only live bookings can be marked as returned.";
                return RedirectToPage(new { tab = "live" });
            }

            /*
                IMPORTANT:
                This calculates late return penalty.

                The function GetOverdueHours() already includes the 45-minute grace period.
                If the customer is less than 45 minutes late, overdueHours will be 0.
            */
            var overdueHours = GetOverdueHours(booking.EndDate, now);

            // Final penalty amount based on overdue hours.
            var penaltyAmount = overdueHours * PenaltyPerHour;

            /*
                IMPORTANT:
                Admin marks the vehicle as returned.

                Status flow:
                started -> completed
            */
            booking.Status = "completed";

            // Once returned, the car becomes available again unless inactive/maintenance.
            if (booking.Car != null &&
                !SameStatus(booking.Car.Status, "maintenance") &&
                !SameStatus(booking.Car.Status, "inactive"))
            {
                booking.Car.Status = "available";
            }

            /*
                IMPORTANT:
                USER NOTIFICATION WHEN BOOKING IS COMPLETED

                This tells the customer that the rental transaction is finished.
            */
            _context.Notifications.Add(new Notification
            {
                RecipientType = "user",
                UserId = booking.UserId,
                Title = "Booking Completed",
                Message = $"Your booking #{booking.BookingId} has been completed.",
                Type = "booking",
                TargetUrl = "/User/Bookings/Index?Tab=history",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            if (penaltyAmount > 0)
            {
                // Check if this booking already has a penalty record.
                var existingPenalty = await _context.Penalties
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);

                if (existingPenalty == null)
                {
                    // Create a new penalty record.
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
                    // Update the existing penalty record.
                    existingPenalty.OverdueHours = overdueHours;
                    existingPenalty.RatePerHour = PenaltyPerHour;
                    existingPenalty.Amount = penaltyAmount;
                    existingPenalty.Status = "unpaid";
                    existingPenalty.CreatedAt = DateTime.Now;
                }

                /*
                    IMPORTANT:
                    ADMIN NOTIFICATION WHEN PENALTY IS CREATED

                    RecipientType = "admin"
                    UserId = null because this notification is for the admin side.
                */
                _context.Notifications.Add(new Notification
                {
                    RecipientType = "admin",
                    UserId = null,
                    Title = "Unpaid Penalty Created",
                    Message = $"Booking #{booking.BookingId} has a penalty of ₱{penaltyAmount:N2}.",
                    Type = "penalty",
                    TargetUrl = "/Admin/Bookings/BookingList?tab=unpaid",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });

                /*
                    IMPORTANT:
                    USER NOTIFICATION WHEN PENALTY IS CREATED

                    This tells the customer that a late return penalty was charged.
                */
                _context.Notifications.Add(new Notification
                {
                    RecipientType = "user",
                    UserId = booking.UserId,
                    Title = "Late Return Penalty",
                    Message = $"Your booking #{booking.BookingId} has a penalty of ₱{penaltyAmount:N2}.",
                    Type = "penalty",
                    TargetUrl = "/User/Bookings/Index?Tab=history",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
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
            // Find penalty record by BookingId.
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

            /*
                IMPORTANT:
                Admin confirms that the customer already paid the penalty.

                Penalty status flow:
                unpaid -> paid
            */
            penalty.Status = "paid";
            penalty.PaidAt = DateTime.Now;

            // Find booking to know which user should receive the notification.
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == penalty.BookingId);

            if (booking != null)
            {
                /*
                    IMPORTANT:
                    USER NOTIFICATION WHEN PENALTY PAYMENT IS CONFIRMED

                    This tells the customer that admin already marked the penalty as paid.
                */
                _context.Notifications.Add(new Notification
                {
                    RecipientType = "user",
                    UserId = booking.UserId,
                    Title = "Penalty Payment Confirmed",
                    Message = $"Your penalty for booking #{booking.BookingId} has been marked as paid.",
                    Type = "penalty",
                    TargetUrl = "/User/Bookings/Index?Tab=history",
                    IsRead = false,
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Penalty payment marked as paid successfully.";
            return RedirectToPage(new { tab = "unpaid" });
        }

        private async Task LoadPageDataAsync()
        {
            var now = DateTime.Now;
            var todayStart = DateTime.Today;
            var tomorrowStart = todayStart.AddDays(1);

            // Normalize query string values to avoid invalid tab/view/stat values.
            ViewMode = NormalizeViewMode(ViewMode);
            StatType = NormalizeStatType(StatType);
            StatRange = NormalizeStatRange(StatRange);

            // Load all bookings with related car and user.
            var allBookings = await _context.Bookings
                .Include(b => b.Car)
                .Include(b => b.User)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            // Get all booking IDs for related table queries.
            var bookingIds = allBookings
                .Select(b => b.BookingId)
                .ToList();

            // Load agreements connected to the current bookings.
            var agreements = await _context.RentalAgreements
                .Where(a => bookingIds.Contains(a.BookingId))
                .ToListAsync();

            // Load payments connected to the current bookings.
            var payments = await _context.Payments
                .Where(p => bookingIds.Contains(p.BookingId))
                .ToListAsync();

            // Load penalties connected to the current bookings.
            var penalties = await _context.Penalties
                .Where(p => bookingIds.Contains(p.BookingId))
                .ToListAsync();

            // Get completed bookings that still have unpaid penalty.
            var unpaidPenaltyBookingIds = penalties
                .Where(p => p.Amount > 0 && !SameStatus(p.Status, "paid"))
                .Select(p => p.BookingId)
                .ToHashSet();

            // Count values for stat cards.
            LiveCount = allBookings.Count(b => SameStatus(b.Status, "started"));
            UpcomingCount = allBookings.Count(b => SameStatus(b.Status, "approved"));
            PendingCount = allBookings.Count(b => SameStatus(b.Status, "pending"));
            CompletedCount = allBookings.Count(b => SameStatus(b.Status, "completed"));
            CancelledCount = allBookings.Count(b => SameStatus(b.Status, "cancelled"));
            OverdueCount = allBookings.Count(b => SameStatus(b.Status, "started") && b.EndDate < now);

            // Count completed bookings with unpaid penalties.
            UnpaidPenaltyCount = allBookings.Count(b =>
                SameStatus(b.Status, "completed") &&
                unpaidPenaltyBookingIds.Contains(b.BookingId)
            );

            // Find agreements approved today.
            var todayApprovedBookingIds = agreements
                .Where(a =>
                    a.ApprovedAt.HasValue &&
                    a.ApprovedAt.Value >= todayStart &&
                    a.ApprovedAt.Value < tomorrowStart
                )
                .Select(a => a.BookingId)
                .ToHashSet();

            // Calculate today's income from bookings approved today.
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

            // Build graph/chart data.
            BuildChartData(allBookings, agreements, penalties, now);

            // Normalize selected tab.
            var selectedTab = NormalizeTab(Tab);
            Tab = selectedTab;

            // Filter bookings based on selected tab.
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

            // Apply search filter.
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

            // Convert Booking database records into AdminBookingListItem view models.
            Bookings = filteredBookings.Select(b =>
            {
                var agreement = agreements.FirstOrDefault(a => a.BookingId == b.BookingId);
                var payment = payments.FirstOrDefault(p => p.BookingId == b.BookingId);
                var savedPenalty = penalties.FirstOrDefault(p => p.BookingId == b.BookingId);

                // Calculate live overdue penalty preview for started bookings.
                var liveOverdueHours = GetOverdueHours(b.EndDate, now);
                var livePenaltyAmount = liveOverdueHours * PenaltyPerHour;

                // Check if booking is already completed.
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
            This method builds the data used by Chart.js.

            It creates labels and values for:
            - Income chart
            - Live booking line
            - Upcoming booking line
            - Overdue booking line
            - Unpaid penalty line
            - Completed booking line
            - Cancelled booking line
        */
        private void BuildChartData(
            List<Booking> allBookings,
            List<RentalAgreement> agreements,
            List<Penalty> penalties,
            DateTime now)
        {
            // Build the date buckets depending on selected range.
            var buckets = BuildDateBuckets(now, StatRange);

            // Chart labels shown at the bottom of the graph.
            var labels = buckets.Select(b => b.Label).ToList();

            // Chart data containers.
            var incomeData = new List<double>();
            var liveData = new List<int>();
            var upcomingData = new List<int>();
            var overdueData = new List<int>();
            var unpaidPenaltyData = new List<int>();
            var completedData = new List<int>();
            var cancelledData = new List<int>();

            foreach (var bucket in buckets)
            {
                // Get booking IDs approved inside this date bucket.
                var approvedIds = agreements
                    .Where(a =>
                        a.ApprovedAt.HasValue &&
                        a.ApprovedAt.Value >= bucket.Start &&
                        a.ApprovedAt.Value < bucket.End
                    )
                    .Select(a => a.BookingId)
                    .ToHashSet();

                // Income data for this bucket.
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

                // Live booking count for this bucket.
                liveData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "started") &&
                    b.StartDate >= bucket.Start &&
                    b.StartDate < bucket.End));

                // Upcoming booking count for this bucket.
                upcomingData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "approved") &&
                    b.CreatedAt >= bucket.Start &&
                    b.CreatedAt < bucket.End));

                // Overdue booking count for this bucket.
                overdueData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "started") &&
                    b.EndDate < now &&
                    b.EndDate >= bucket.Start &&
                    b.EndDate < bucket.End));

                // Completed booking count for this bucket.
                completedData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "completed") &&
                    b.EndDate >= bucket.Start &&
                    b.EndDate < bucket.End));

                // Cancelled booking count for this bucket.
                cancelledData.Add(allBookings.Count(b =>
                    SameStatus(b.Status, "cancelled") &&
                    b.CreatedAt >= bucket.Start &&
                    b.CreatedAt < bucket.End));

                // Unpaid penalty count for this bucket.
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

            // Convert all chart data into JSON strings for Chart.js.
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

            // Today graph uses 4-hour intervals.
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

            // Weekly graph uses last 7 days.
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

            // Monthly graph uses every day of the current month.
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

            // Overall graph uses last 12 months.
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

        // Converts C# list into JSON string.
        private static string ToJson<T>(List<T> values)
        {
            return System.Text.Json.JsonSerializer.Serialize(values);
        }

        // Makes sure only valid tab names are accepted.
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

        // Makes sure view mode is either bookings or stats.
        private static string NormalizeViewMode(string? viewMode)
        {
            return viewMode?.ToLower().Trim() == "stats"
                ? "stats"
                : "bookings";
        }

        // Makes sure stat type is either income or status.
        private static string NormalizeStatType(string? statType)
        {
            return statType?.ToLower().Trim() switch
            {
                "income" => "income",
                _ => "status"
            };
        }

        // Makes sure stat range is valid.
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

        // Converts status into lowercase format.
        private static string NormalizeStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? "pending"
                : status.ToLower().Trim();
        }

        // Compares status safely and ignores uppercase/lowercase differences.
        private static bool SameStatus(string? value, string expected)
        {
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        /*
            IMPORTANT:
            This method calculates penalty hours.

            It applies the 45-minute grace period.
            If the customer is less than 45 minutes late, it returns 0.
            If the customer is 45 minutes or more late, it rounds up the hours.
        */
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

        // Converts database booking status into display text.
        private static string GetDisplayStatus(string? status, DateTime endDate, DateTime now)
        {
            var normalizedStatus = NormalizeStatus(status);

            // A started booking becomes visually overdue if the end date already passed.
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