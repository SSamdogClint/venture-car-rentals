using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Payments
{
    public class PaymentDetailsModel : PageModel
    {
        private readonly AppDbContext _context;

        public PaymentDetailsModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PaymentStatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? PaymentMethodFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public DateTime? ToDate { get; set; }

        public List<PaymentDetailsRowViewModel> Payments { get; set; } = new();

        public List<string> PaymentMethods { get; set; } = new();

        public int TotalPayments { get; set; }
        public int PaidPayments { get; set; }
        public int PendingPayments { get; set; }
        public int RefundedPayments { get; set; }

        public double TotalCollectedAmount { get; set; }
        public double PendingAmount { get; set; }
        public double RefundedAmount { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadPaymentDetailsAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync()
        {
            await LoadPaymentDetailsAsync();

            /*
                IMPORTANT:
                QuestPDF requires a license setting.
                Community is free for eligible use cases.
            */
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = GeneratePaymentDetailsPdf();

            var fileName = $"payment-details-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task LoadPaymentDetailsAsync()
        {
            /*
                IMPORTANT:
                Load Payment with Booking, User, and Car.

                This lets the admin see:
                - renter name
                - car name
                - booking status
                - payment status
                - paid date
            */
            var paymentQuery = _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b!.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Car)
                .AsNoTracking()
                .AsQueryable();

            var paymentList = await paymentQuery
                .OrderByDescending(p => p.PaymentId)
                .ToListAsync();

            /*
                IMPORTANT:
                Convert to ViewModel first so filtering is simpler and safe.
            */
            var rows = paymentList.Select(payment => new PaymentDetailsRowViewModel
            {
                PaymentId = payment.PaymentId,
                BookingId = payment.BookingId,

                RenterName = payment.Booking?.User == null
                    ? "Unknown Renter"
                    : $"{payment.Booking.User.FirstName} {payment.Booking.User.LastName}",

                RenterEmail = payment.Booking?.User?.Email ?? "",

                CarName = payment.Booking?.Car == null
                    ? "Unknown Car"
                    : $"{payment.Booking.Car.Make} {payment.Booking.Car.Model}",

                CarCategory = payment.Booking?.Car?.Category ?? "",

                StartDate = payment.Booking?.StartDate,
                EndDate = payment.Booking?.EndDate,

                BookingStatus = payment.Booking?.Status ?? "unknown",

                PaymentMethod = payment.PaymentMethod ?? "",
                PaymentStatus = payment.PaymentStatus ?? "",

                Amount = payment.Amount,
                PaidAt = payment.PaidAt,

                BookingCreatedAt = payment.Booking?.CreatedAt
            }).ToList();

            PaymentMethods = rows
                .Where(p => !string.IsNullOrWhiteSpace(p.PaymentMethod))
                .Select(p => p.PaymentMethod)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim().ToLower();

                rows = rows.Where(p =>
                    p.RenterName.ToLower().Contains(keyword) ||
                    p.RenterEmail.ToLower().Contains(keyword) ||
                    p.CarName.ToLower().Contains(keyword) ||
                    p.CarCategory.ToLower().Contains(keyword) ||
                    p.BookingId.ToString().Contains(keyword) ||
                    p.PaymentId.ToString().Contains(keyword)
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(PaymentStatusFilter))
            {
                rows = rows
                    .Where(p => p.PaymentStatus.Equals(PaymentStatusFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(PaymentMethodFilter))
            {
                rows = rows
                    .Where(p => p.PaymentMethod.Equals(PaymentMethodFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (FromDate != null)
            {
                rows = rows
                    .Where(p => p.BookingCreatedAt != null &&
                                p.BookingCreatedAt.Value.Date >= FromDate.Value.Date)
                    .ToList();
            }

            if (ToDate != null)
            {
                rows = rows
                    .Where(p => p.BookingCreatedAt != null &&
                                p.BookingCreatedAt.Value.Date <= ToDate.Value.Date)
                    .ToList();
            }

            Payments = rows;

            LoadStatistics(rows);
        }

        private void LoadStatistics(List<PaymentDetailsRowViewModel> rows)
        {
            TotalPayments = rows.Count;

            PaidPayments = rows.Count(p =>
                p.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase));

            RefundedPayments = rows.Count(p =>
                p.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase));

            PendingPayments = rows.Count(p =>
                !p.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
                !p.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase));

            /*
                IMPORTANT:
                This is the collected income only.

                Pending, unpaid, and pending_admin_approval payments are NOT counted
                as income because they are not completed yet.
            */
            TotalCollectedAmount = rows
                .Where(p => p.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);

            PendingAmount = rows
                .Where(p =>
                    !p.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
                    !p.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);

            RefundedAmount = rows
                .Where(p => p.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
                .Sum(p => p.Amount);
        }

        private byte[] GeneratePaymentDetailsPdf()
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

                        header.Item().Text("Payment Details Report")
                            .FontSize(14)
                            .SemiBold();

                        header.Item().Text($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(content =>
                    {
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Total Payments: {TotalPayments}").Bold();
                            row.RelativeItem().Text($"Paid: {PaidPayments}").Bold();
                            row.RelativeItem().Text($"Pending: {PendingPayments}").Bold();
                            row.RelativeItem().Text($"Refunded: {RefundedPayments}").Bold();
                        });

                        content.Item().PaddingVertical(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Collected Amount: PHP {TotalCollectedAmount:N2}").Bold();
                            row.RelativeItem().Text($"Pending Amount: PHP {PendingAmount:N2}");
                            row.RelativeItem().Text($"Refunded Amount: PHP {RefundedAmount:N2}");
                        });

                        content.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(90);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Pay ID");
                                header.Cell().Element(HeaderCell).Text("Book ID");
                                header.Cell().Element(HeaderCell).Text("Renter");
                                header.Cell().Element(HeaderCell).Text("Vehicle");
                                header.Cell().Element(HeaderCell).Text("Method");
                                header.Cell().Element(HeaderCell).Text("Status");
                                header.Cell().Element(HeaderCell).Text("Amount");
                                header.Cell().Element(HeaderCell).Text("Collected");
                                header.Cell().Element(HeaderCell).Text("Paid At");
                            });

                            foreach (var payment in Payments)
                            {
                                table.Cell().Element(BodyCell).Text(payment.PaymentId.ToString());
                                table.Cell().Element(BodyCell).Text(payment.BookingId.ToString());
                                table.Cell().Element(BodyCell).Text(payment.RenterName);
                                table.Cell().Element(BodyCell).Text(payment.CarName);
                                table.Cell().Element(BodyCell).Text(payment.PaymentMethod);
                                table.Cell().Element(BodyCell).Text(payment.DisplayPaymentStatus);
                                table.Cell().Element(BodyCell).Text($"PHP {payment.Amount:N2}");

                                /*
                                    IMPORTANT:
                                    Collected amount is shown only when payment is paid.
                                */
                                table.Cell().Element(BodyCell).Text(payment.IsPaid ? $"PHP {payment.Amount:N2}" : "Not collected");

                                table.Cell().Element(BodyCell).Text(payment.PaidAt == null
                                    ? "Not paid"
                                    : payment.PaidAt.Value.ToString("MMM dd, yyyy"));
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

    public class PaymentDetailsRowViewModel
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }

        public string RenterName { get; set; } = "";
        public string RenterEmail { get; set; } = "";

        public string CarName { get; set; } = "";
        public string CarCategory { get; set; } = "";

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string BookingStatus { get; set; } = "";

        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";

        public double Amount { get; set; }

        public DateTime? PaidAt { get; set; }
        public DateTime? BookingCreatedAt { get; set; }

        public bool IsPaid =>
            PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase);

        public string DisplayPaymentStatus =>
            string.IsNullOrWhiteSpace(PaymentStatus)
                ? "Unknown"
                : PaymentStatus.Replace("_", " ");

        public string DisplayPaymentMethod =>
            string.IsNullOrWhiteSpace(PaymentMethod)
                ? "Unknown"
                : PaymentMethod.Replace("_", " ");

        public string DisplayBookingStatus =>
            string.IsNullOrWhiteSpace(BookingStatus)
                ? "Unknown"
                : BookingStatus.Replace("_", " ");
    }
}