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

        // IMPORTANT SS THIS:
        // This is the penalty rule used by the booking system.
        //
        // Rule:
        // - If the customer is late for less than 45 minutes, no penalty will be charged.
        // - If the customer is late for 45 minutes or more, the system charges ₱200 per started hour.
        //
        // Example:
        // - 30 minutes late = ₱0
        // - 45 minutes late = ₱200
        // - 1 hour 10 minutes late = ₱400 because it counts as 2 started hours
        private const double PenaltyPerHour = 200;
        private const int PenaltyGraceMinutes = 45;

        public BookingListModel(AppDbContext context, IWebHostEnvironment environment)
        {
            // Database context for accessing Bookings, Cars, Payments, Penalties, Notifications, etc.
            _context = context;

            // Used for saving uploaded signed rental agreement files inside wwwroot.
            _environment = environment;
        }

        // List displayed in the admin booking table.
        public List<AdminBookingListItem> Bookings { get; set; } = new();

        // Current active tab: live, upcoming, pending, history, unpaid.
        [BindProperty(SupportsGet = true)]
        public string Tab { get; set; } = "live";

        // IMPORTANT SS THIS:
        // ViewMode controls what appears on the right section of the admin booking page.
        //
        // bookings = normal table and tabs
        // stats = statistics graph panel
        [BindProperty(SupportsGet = true)]
        public string ViewMode { get; set; } = "bookings";

        // IMPORTANT SS THIS:
        // StatType controls which graph is shown.
        //
        // income = income graph
        // status = booking status comparison graph
        [BindProperty(SupportsGet = true)]
        public string StatType { get; set; } = "status";

        // IMPORTANT SS THIS:
        // StatRange controls the graph date range.
        //
        // today = hourly buckets
        // weekly = last 7 days
        // monthly = current month days
        // overall = last 12 months
        [BindProperty(SupportsGet = true)]
        public string StatRange { get; set; } = "today";

        // Search input from admin search bar.
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        // Booking ID used by post actions such as approve, start, return, cancel, mark penalty paid.
        [BindProperty]
        public int BookingId { get; set; }

        // IMPORTANT SS THIS:
        // This uploaded file is now used directly by Approve Booking.
        // The admin no longer needs to click a separate Upload Agreement button.
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

        public bool IsRefundEligible { get; set; }
        public bool HasRefund { get; set; }

        public string RefundStatus { get; set; } = "";
        public double RefundAmount { get; set; }
        public double NonRefundableAmount { get; set; }
        public DateTime? RefundedAt { get; set; }

        public async Task OnGetAsync()
        {
            // Loads table data, stat cards, and graph data.
            await LoadPageDataAsync();
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

            // IMPORTANT SS THIS:
            // This uploads the signed agreement at the same time the admin clicks Approve Booking.
            // This replaces the old separate Upload Agreement button.
            if (SignedAgreementFile != null && SignedAgreementFile.Length > 0)
            {
                var uploadResult = await SaveSignedAgreementFileAsync(agreement, booking.BookingId);

                if (!uploadResult.Success)
                {
                    TempData["Error"] = uploadResult.ErrorMessage;
                    return RedirectToPage(new { tab = "pending" });
                }
            }

            // IMPORTANT SS THIS:
            // Booking cannot be approved unless a signed agreement file already exists
            // or the admin selected a file during approval.
            if (string.IsNullOrWhiteSpace(agreement.SignedAgreementFileUrl))
            {
                TempData["Error"] = "Please select and upload the signed rental agreement before approving this booking.";
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

            // Find the payment connected to this booking.
            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found. Booking cannot be approved.";
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

            // IMPORTANT SS THIS:
            // This is where the admin officially approves the booking.
            //
            // Status flow:
            // pending -> approved
            //
            // After this, the booking appears under Upcoming Bookings.
            // It will not become live until admin clicks Start Booking.
            booking.Status = "approved";

            // IMPORTANT SS THIS:
            // The agreement becomes approved only after the signed file exists.
            agreement.Status = "approved";
            agreement.ApprovedAt = DateTime.Now;

            // Mark the car as booked because it already has an approved schedule.
            booking.Car.Status = "booked";

            // IMPORTANT SS THIS:
            // Any payment method automatically becomes paid after admin approval.
            //
            // This applies to:
            // - cash pickup
            // - saved card pickup
            // - gcash or any other future method
            //
            // This prevents Payment Details and Transactions from still showing
            // pending_admin_approval after the booking is already approved.
            payment.PaymentStatus = "paid";
            payment.PaidAt = DateTime.Now;

            // Notify the customer that the booking has been approved.
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

            // Saves booking update, car update, agreement update, payment update, and notification insert.
            await _context.SaveChangesAsync();

            TempData["Success"] = "Booking approved successfully. Signed agreement uploaded and payment marked as paid.";
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

            // IMPORTANT SS THIS:
            // Admin manually starts the booking.
            //
            // Status flow:
            // approved -> started
            //
            // This is why the live booking will not start automatically.
            // It only starts when admin clicks the Start Booking button.
            booking.Status = "started";

            // Save the actual start time as the time admin clicked Start Booking.
            booking.StartDate = now;

            // Keep the car as booked while the customer is using it.
            booking.Car.Status = "booked";

            // Notify the customer that the booking is now live.
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

            // IMPORTANT SS THIS:
            // Admin cancels the booking.
            //
            // Status flow:
            // pending/approved -> cancelled
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

            // Notify the customer that admin cancelled the booking.
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

            // IMPORTANT SS THIS:
            // This calculates late return penalty.
            //
            // The function GetOverdueHours() already includes the 45-minute grace period.
            // If the customer is less than 45 minutes late, overdueHours will be 0.
            var overdueHours = GetOverdueHours(booking.EndDate, now);

            // Final penalty amount based on overdue hours.
            var penaltyAmount = overdueHours * PenaltyPerHour;

            // IMPORTANT SS THIS:
            // Admin marks the vehicle as returned.
            //
            // Status flow:
            // started -> completed
            booking.Status = "completed";

            // Once returned, the car becomes available again unless inactive/maintenance.
            if (booking.Car != null &&
                !SameStatus(booking.Car.Status, "maintenance") &&
                !SameStatus(booking.Car.Status, "inactive"))
            {
                booking.Car.Status = "available";
            }

            // Notify the customer that the booking is completed.
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

                // Notify admin that an unpaid penalty was created.
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

                // Notify customer that a late return penalty was charged.
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

            // IMPORTANT SS THIS:
            // Admin confirms that the customer already paid the penalty.
            //
            // Penalty status flow:
            // unpaid -> paid
            penalty.Status = "paid";
            penalty.PaidAt = DateTime.Now;

            // Find booking to know which user should receive the notification.
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == penalty.BookingId);

            if (booking != null)
            {
                // Notify the customer that the penalty payment was confirmed.
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

        // IMPORTANT SS THIS:
        // This helper saves the selected signed agreement file during Approve Booking.
        // It validates:
        // - file exists
        // - file extension
        // - file size
        // - wwwroot path
        // Then it saves the file URL in RentalAgreement.
        private async Task<FileUploadResult> SaveSignedAgreementFileAsync(RentalAgreement agreement, int bookingId)
        {
            if (SignedAgreementFile == null || SignedAgreementFile.Length == 0)
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "Please select a signed agreement file before approving this booking."
                };
            }

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(SignedAgreementFile.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "Invalid file type. Only PDF, JPG, JPEG, and PNG files are allowed."
                };
            }

            // Limit upload to 5 MB.
            var maxFileSize = 5 * 1024 * 1024;

            if (SignedAgreementFile.Length > maxFileSize)
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "File size is too large. Maximum allowed size is 5 MB."
                };
            }

            var webRootPath = _environment.WebRootPath;

            if (string.IsNullOrWhiteSpace(webRootPath))
            {
                return new FileUploadResult
                {
                    Success = false,
                    ErrorMessage = "Web root path is not available. Please check your wwwroot folder."
                };
            }

            var uploadFolder = Path.Combine(webRootPath, "uploads", "rental-agreements", "signed");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var fileName = $"signed_agreement_booking_{bookingId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await SignedAgreementFile.CopyToAsync(stream);
            }

            // IMPORTANT SS THIS:
            // This connects the uploaded signed agreement file to the rental agreement record.
            agreement.SignedAgreementFileUrl = $"/uploads/rental-agreements/signed/{fileName}";
            agreement.SignedUploadedAt = DateTime.Now;
            agreement.Status = "signed_uploaded";

            return new FileUploadResult
            {
                Success = true,
                ErrorMessage = ""
            };
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

    public class FileUploadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}