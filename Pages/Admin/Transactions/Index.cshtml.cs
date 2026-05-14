using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Transactions
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string Period { get; set; } = "all";

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public List<TransactionRowViewModel> Transactions { get; set; } = new();

        public int TotalTransactions { get; set; }
        public int PaidTransactions { get; set; }
        public int PendingTransactions { get; set; }
        public int RefundedTransactions { get; set; }

        public double TotalAmount { get; set; }
        public double CollectedAmount { get; set; }
        public double PendingAmount { get; set; }
        public double RefundedAmount { get; set; }

        public string PeriodTitle { get; set; } = "All Transactions";

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadTransactionsAsync(Period);
            return Page();
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync(string period = "all")
        {
            await LoadTransactionsAsync(period);

            /*
                IMPORTANT:
                QuestPDF needs a license setting before generating PDF.
                Community license is enough for student/small project use.
            */
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = GenerateTransactionPdf();

            var fileName = $"transactions-{period}-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task LoadTransactionsAsync(string selectedPeriod)
        {
            Period = string.IsNullOrWhiteSpace(selectedPeriod)
                ? "all"
                : selectedPeriod.ToLower().Trim();

            var today = DateTime.Today;

            DateTime? fromDate = null;
            DateTime? toDate = null;

            /*
                IMPORTANT:
                Period controls what transaction records will be displayed.

                today   = transactions created today
                weekly  = transactions from the last 7 days
                monthly = transactions from the current month
                all     = all transaction records
            */
            if (Period == "today")
            {
                fromDate = today;
                toDate = today.AddDays(1);
                PeriodTitle = "Today's Transactions";
            }
            else if (Period == "weekly")
            {
                fromDate = today.AddDays(-7);
                toDate = today.AddDays(1);
                PeriodTitle = "Weekly Transactions";
            }
            else if (Period == "monthly")
            {
                fromDate = new DateTime(today.Year, today.Month, 1);
                toDate = fromDate.Value.AddMonths(1);
                PeriodTitle = "Monthly Transactions";
            }
            else
            {
                Period = "all";
                PeriodTitle = "All Transactions";
            }

            /*
                IMPORTANT:
                This page treats Payment records as transaction records.

                It loads Payment with Booking, User, and Car so the admin can see
                who paid, what car was booked, and the current payment status.
            */
            var query = _context.Payments
                .Include(p => p.Booking)
                    .ThenInclude(b => b!.User)
                .Include(p => p.Booking)
                    .ThenInclude(b => b!.Car)
                .AsNoTracking()
                .AsQueryable();

            if (fromDate != null && toDate != null)
            {
                query = query.Where(p =>
                    p.Booking != null &&
                    p.Booking.CreatedAt >= fromDate.Value &&
                    p.Booking.CreatedAt < toDate.Value);
            }

            var payments = await query
                .OrderByDescending(p => p.PaymentId)
                .ToListAsync();

            var rows = payments.Select(payment => new TransactionRowViewModel
            {
                TransactionId = payment.PaymentId,
                PaymentId = payment.PaymentId,
                BookingId = payment.BookingId,

                RenterName = payment.Booking?.User == null
                    ? "Unknown Renter"
                    : $"{payment.Booking.User.FirstName} {payment.Booking.User.LastName}",

                RenterEmail = payment.Booking?.User?.Email ?? "",

                Vehicle = payment.Booking?.Car == null
                    ? "Unknown Vehicle"
                    : $"{payment.Booking.Car.Make} {payment.Booking.Car.Model}",

                VehicleCategory = payment.Booking?.Car?.Category ?? "",

                PaymentMethod = payment.PaymentMethod ?? "",
                PaymentStatus = payment.PaymentStatus ?? "",

                BookingStatus = payment.Booking?.Status ?? "",
                Amount = payment.Amount,
                PaidAt = payment.PaidAt,

                TransactionDate = payment.PaidAt ?? payment.Booking?.CreatedAt ?? DateTime.MinValue,

                StartDate = payment.Booking?.StartDate,
                EndDate = payment.Booking?.EndDate
            }).ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim().ToLower();

                rows = rows.Where(t =>
                    t.TransactionId.ToString().Contains(keyword) ||
                    t.BookingId.ToString().Contains(keyword) ||
                    t.RenterName.ToLower().Contains(keyword) ||
                    t.RenterEmail.ToLower().Contains(keyword) ||
                    t.Vehicle.ToLower().Contains(keyword) ||
                    t.PaymentMethod.ToLower().Contains(keyword) ||
                    t.PaymentStatus.ToLower().Contains(keyword)
                ).ToList();
            }

            Transactions = rows;

            LoadStatistics(rows);
        }

        private void LoadStatistics(List<TransactionRowViewModel> rows)
        {
            TotalTransactions = rows.Count;

            PaidTransactions = rows.Count(t =>
                t.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase));

            RefundedTransactions = rows.Count(t =>
                t.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase));

            PendingTransactions = rows.Count(t =>
                !t.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
                !t.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase));

            TotalAmount = rows.Sum(t => t.Amount);

            /*
                IMPORTANT:
                CollectedAmount counts only paid transactions.
                Pending and pending_admin_approval are not counted as collected income.
            */
            CollectedAmount = rows
                .Where(t => t.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            PendingAmount = rows
                .Where(t =>
                    !t.PaymentStatus.Equals("paid", StringComparison.OrdinalIgnoreCase) &&
                    !t.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            RefundedAmount = rows
                .Where(t => t.PaymentStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);
        }

        private byte[] GenerateTransactionPdf()
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

                        header.Item().Text($"{PeriodTitle} Report")
                            .FontSize(14)
                            .SemiBold();

                        header.Item().Text($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(content =>
                    {
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Total Transactions: {TotalTransactions}").Bold();
                            row.RelativeItem().Text($"Paid: {PaidTransactions}").Bold();
                            row.RelativeItem().Text($"Pending: {PendingTransactions}").Bold();
                            row.RelativeItem().Text($"Refunded: {RefundedTransactions}").Bold();
                        });

                        content.Item().PaddingVertical(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Collected: PHP {CollectedAmount:N2}").Bold();
                            row.RelativeItem().Text($"Pending: PHP {PendingAmount:N2}");
                            row.RelativeItem().Text($"Refunded: PHP {RefundedAmount:N2}");
                        });

                        content.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(50);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(90);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Txn ID");
                                header.Cell().Element(HeaderCell).Text("Book ID");
                                header.Cell().Element(HeaderCell).Text("Renter");
                                header.Cell().Element(HeaderCell).Text("Vehicle");
                                header.Cell().Element(HeaderCell).Text("Method");
                                header.Cell().Element(HeaderCell).Text("Status");
                                header.Cell().Element(HeaderCell).Text("Amount");
                                header.Cell().Element(HeaderCell).Text("Collected");
                                header.Cell().Element(HeaderCell).Text("Date");
                            });

                            foreach (var transaction in Transactions)
                            {
                                table.Cell().Element(BodyCell).Text($"#{transaction.TransactionId}");
                                table.Cell().Element(BodyCell).Text($"#{transaction.BookingId}");
                                table.Cell().Element(BodyCell).Text(transaction.RenterName);
                                table.Cell().Element(BodyCell).Text(transaction.Vehicle);
                                table.Cell().Element(BodyCell).Text(transaction.DisplayPaymentMethod);
                                table.Cell().Element(BodyCell).Text(transaction.DisplayPaymentStatus);
                                table.Cell().Element(BodyCell).Text($"PHP {transaction.Amount:N2}");

                                table.Cell().Element(BodyCell).Text(transaction.IsPaid
                                    ? $"PHP {transaction.Amount:N2}"
                                    : "Not collected");

                                table.Cell().Element(BodyCell).Text(transaction.TransactionDate == DateTime.MinValue
                                    ? "N/A"
                                    : transaction.TransactionDate.ToString("MMM dd, yyyy"));
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

    public class TransactionRowViewModel
    {
        public int TransactionId { get; set; }
        public int PaymentId { get; set; }
        public int BookingId { get; set; }

        public string RenterName { get; set; } = "";
        public string RenterEmail { get; set; } = "";

        public string Vehicle { get; set; } = "";
        public string VehicleCategory { get; set; } = "";

        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string BookingStatus { get; set; } = "";

        public double Amount { get; set; }

        public DateTime? PaidAt { get; set; }
        public DateTime TransactionDate { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

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