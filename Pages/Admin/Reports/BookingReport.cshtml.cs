using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Reports
{
    public class BookingReportModel : PageModel
    {
        private readonly AppDbContext _context;

        public BookingReportModel(AppDbContext context)
        {
            _context = context;
        }

        /*
            IMPORTANT:
            Period controls the report range.

            today   = bookings created today
            weekly  = bookings created within the current week
            monthly = bookings created within the current month
            overall = all bookings
        */
        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "overall";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<AdminBookingReportRow> Bookings { get; set; } = new();

        public int TotalBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }
        public int OverdueBookings { get; set; }

        public double CollectedAmount { get; set; }
        public double TotalBookingAmount { get; set; }
        public double PendingAmount { get; set; }

        public string PeriodTitle { get; set; } = "Overall Booking Report";

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadBookingReportAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync()
        {
            await LoadBookingReportAsync();

            /*
                IMPORTANT:
                QuestPDF requires license setup before generating PDF.
                Community license is suitable for student/small project use.
            */
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = GenerateBookingReportPdf();
            var fileName = $"booking-report-{Period}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task LoadBookingReportAsync()
        {
            NormalizePeriod();

            var dateRange = GetDateRangeByPeriod();

            /*
                IMPORTANT:
                Load bookings with related User and Car.

                This allows the report table to show:
                - customer name
                - customer email
                - vehicle name
                - vehicle plate number
                - booking schedule
            */
            var query = _context.Bookings
                .Include(b => b.User)
                .Include(b => b.Car)
                .AsNoTracking()
                .AsQueryable();

            if (dateRange.FromDate != null && dateRange.ToDate != null)
            {
                query = query.Where(b =>
                    b.CreatedAt >= dateRange.FromDate.Value &&
                    b.CreatedAt < dateRange.ToDate.Value);
            }

            var allBookings = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var bookingIds = allBookings
                .Select(b => b.BookingId)
                .ToList();

            /*
                IMPORTANT:
                Load payment records separately.

                Payment status is used for collected and pending amount.
            */
            var payments = await _context.Payments
                .AsNoTracking()
                .Where(p => bookingIds.Contains(p.BookingId))
                .ToListAsync();

            /*
                IMPORTANT:
                Load rental agreement records separately.

                This lets the report display agreement status without requiring
                extra navigation properties.
            */
            var agreements = await _context.RentalAgreements
                .AsNoTracking()
                .Where(a => bookingIds.Contains(a.BookingId))
                .ToListAsync();

            var now = DateTime.Now;

            var rows = allBookings.Select(booking =>
            {
                var payment = payments.FirstOrDefault(p => p.BookingId == booking.BookingId);
                var agreement = agreements.FirstOrDefault(a => a.BookingId == booking.BookingId);

                return new AdminBookingReportRow
                {
                    BookingId = booking.BookingId,

                    CustomerName = booking.User == null
                        ? "Unknown Customer"
                        : $"{booking.User.FirstName} {booking.User.LastName}",

                    CustomerEmail = booking.User?.Email ?? "No email",

                    VehicleName = booking.Car == null
                        ? "Unknown Vehicle"
                        : $"{booking.Car.Make} {booking.Car.Model}",

                    VehicleCategory = booking.Car?.Category ?? "",
                    VehiclePlate = booking.Car?.LicensePlate ?? "",

                    StartDate = booking.StartDate,
                    EndDate = booking.EndDate,
                    CreatedAt = booking.CreatedAt,

                    TotalPrice = booking.TotalPrice,
                    BookingStatus = booking.Status,

                    PaymentStatus = payment?.PaymentStatus ?? "No Payment",
                    PaymentMethod = payment?.PaymentMethod ?? "N/A",
                    PaidAt = payment?.PaidAt,

                    AgreementStatus = agreement?.Status ?? "No Agreement",
                    AgreementApprovedAt = agreement?.ApprovedAt,

                    IsOverdue = booking.Status == "started" && booking.EndDate < now
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim();

                rows = rows.Where(b =>
                    b.BookingId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.CustomerName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.CustomerEmail.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.VehicleName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.VehicleCategory.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.VehiclePlate.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.BookingStatus.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    b.PaymentStatus.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                ).ToList();
            }

            Bookings = rows;

            LoadStatistics(rows);
        }

        private void NormalizePeriod()
        {
            Period = string.IsNullOrWhiteSpace(Period)
                ? "overall"
                : Period.ToLower().Trim();

            if (Period != "today" &&
                Period != "weekly" &&
                Period != "monthly" &&
                Period != "overall")
            {
                Period = "overall";
            }
        }

        private ReportDateRange GetDateRangeByPeriod()
        {
            var today = DateTime.Today;

            if (Period == "today")
            {
                PeriodTitle = "Today's Booking Report";

                return new ReportDateRange
                {
                    FromDate = today,
                    ToDate = today.AddDays(1)
                };
            }

            if (Period == "weekly")
            {
                /*
                    IMPORTANT:
                    Weekly report uses the current week from Monday to Sunday.
                */
                var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
                var startOfWeek = today.AddDays(-daysSinceMonday);
                var endOfWeek = startOfWeek.AddDays(7);

                PeriodTitle = "Weekly Booking Report";

                return new ReportDateRange
                {
                    FromDate = startOfWeek,
                    ToDate = endOfWeek
                };
            }

            if (Period == "monthly")
            {
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1);

                PeriodTitle = "Monthly Booking Report";

                return new ReportDateRange
                {
                    FromDate = startOfMonth,
                    ToDate = endOfMonth
                };
            }

            PeriodTitle = "Overall Booking Report";

            return new ReportDateRange
            {
                FromDate = null,
                ToDate = null
            };
        }

        private void LoadStatistics(List<AdminBookingReportRow> rows)
        {
            TotalBookings = rows.Count;

            CompletedBookings = rows.Count(b =>
                b.BookingStatus.Equals("completed", StringComparison.OrdinalIgnoreCase));

            CancelledBookings = rows.Count(b =>
                b.BookingStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase));

            OverdueBookings = rows.Count(b => b.IsOverdue);

            TotalBookingAmount = rows.Sum(b => b.TotalPrice);

            /*
                IMPORTANT:
                CollectedAmount counts only PAID payments.
                Pending, unpaid, pending_admin_approval, and cancelled bookings
                are not counted as collected income.
            */
            CollectedAmount = rows
                .Where(b => b.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
                .Sum(b => b.TotalPrice);

            /*
                IMPORTANT:
                PendingAmount counts bookings that are not paid and not cancelled.
            */
            PendingAmount = rows
                .Where(b =>
                    !b.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
                    !b.BookingStatus.Equals("cancelled", StringComparison.OrdinalIgnoreCase))
                .Sum(b => b.TotalPrice);
        }

        private byte[] GenerateBookingReportPdf()
        {
            using var stream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header().Column(header =>
                    {
                        header.Item().Text("Venture Car Rentals")
                            .FontSize(18)
                            .Bold();

                        header.Item().Text(PeriodTitle)
                            .FontSize(14)
                            .SemiBold();

                        header.Item().Text($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(content =>
                    {
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Total Bookings: {TotalBookings}").Bold();
                            row.RelativeItem().Text($"Completed: {CompletedBookings}").Bold();
                            row.RelativeItem().Text($"Cancelled: {CancelledBookings}").Bold();
                            row.RelativeItem().Text($"Overdue: {OverdueBookings}").Bold();
                        });

                        content.Item().PaddingVertical(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Collected Amount: PHP {CollectedAmount:N2}").Bold();
                            row.RelativeItem().Text($"Total Booking Amount: PHP {TotalBookingAmount:N2}");
                            row.RelativeItem().Text($"Pending Amount: PHP {PendingAmount:N2}");
                        });

                        content.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(85);
                                columns.RelativeColumn(1.1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Booking ID");
                                header.Cell().Element(HeaderCell).Text("Customer");
                                header.Cell().Element(HeaderCell).Text("Vehicle");
                                header.Cell().Element(HeaderCell).Text("Start");
                                header.Cell().Element(HeaderCell).Text("End");
                                header.Cell().Element(HeaderCell).Text("Booking");
                                header.Cell().Element(HeaderCell).Text("Payment");
                                header.Cell().Element(HeaderCell).Text("Agreement");
                                header.Cell().Element(HeaderCell).Text("Amount");
                                header.Cell().Element(HeaderCell).Text("Created");
                            });

                            foreach (var booking in Bookings)
                            {
                                table.Cell().Element(BodyCell).Text($"#{booking.BookingId}");
                                table.Cell().Element(BodyCell).Text(booking.CustomerName);
                                table.Cell().Element(BodyCell).Text(booking.VehicleName);
                                table.Cell().Element(BodyCell).Text(booking.StartDate.ToString("MMM dd, yyyy"));
                                table.Cell().Element(BodyCell).Text(booking.EndDate.ToString("MMM dd, yyyy"));
                                table.Cell().Element(BodyCell).Text(booking.IsOverdue ? "overdue" : booking.DisplayBookingStatus);
                                table.Cell().Element(BodyCell).Text(booking.DisplayPaymentStatus);
                                table.Cell().Element(BodyCell).Text(booking.DisplayAgreementStatus);
                                table.Cell().Element(BodyCell).Text($"PHP {booking.TotalPrice:N2}");
                                table.Cell().Element(BodyCell).Text(booking.CreatedAt.ToString("MMM dd, yyyy"));
                            }
                        });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                        });
                });
            }).GeneratePdf(stream);

            return stream.ToArray();
        }

        private static IContainer HeaderCell(IContainer container)
        {
            return container
                .Background(Colors.Grey.Lighten2)
                .Border(1)
                .BorderColor(Colors.Grey.Lighten1)
                .Padding(4);
        }

        private static IContainer BodyCell(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(4);
        }
    }

    public class ReportDateRange
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class AdminBookingReportRow
    {
        public int BookingId { get; set; }

        public string CustomerName { get; set; } = "";
        public string CustomerEmail { get; set; } = "";

        public string VehicleName { get; set; } = "";
        public string VehicleCategory { get; set; } = "";
        public string VehiclePlate { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public double TotalPrice { get; set; }

        public string BookingStatus { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string PaymentMethod { get; set; } = "";

        public DateTime? PaidAt { get; set; }

        public string AgreementStatus { get; set; } = "";
        public DateTime? AgreementApprovedAt { get; set; }

        public bool IsOverdue { get; set; }

        public string DisplayBookingStatus =>
            string.IsNullOrWhiteSpace(BookingStatus)
                ? "Unknown"
                : BookingStatus.Replace("_", " ");

        public string DisplayPaymentStatus =>
            string.IsNullOrWhiteSpace(PaymentStatus)
                ? "Unknown"
                : PaymentStatus.Replace("_", " ");

        public string DisplayPaymentMethod =>
            string.IsNullOrWhiteSpace(PaymentMethod)
                ? "Unknown"
                : PaymentMethod.Replace("_", " ");

        public string DisplayAgreementStatus =>
            string.IsNullOrWhiteSpace(AgreementStatus)
                ? "Unknown"
                : AgreementStatus.Replace("_", " ");
    }
}