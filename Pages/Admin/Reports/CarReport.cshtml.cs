using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Reports
{
    public class CarReportModel : PageModel
    {
        private readonly AppDbContext _context;

        public CarReportModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? TransmissionFilter { get; set; }

        public List<AdminCarReportRow> Cars { get; set; } = new();

        public List<string> Categories { get; set; } = new();
        public List<string> Transmissions { get; set; } = new();

        public int TotalCars { get; set; }
        public int AvailableCars { get; set; }
        public int BookedCars { get; set; }
        public int MaintenanceCars { get; set; }
        public int InactiveCars { get; set; }

        public double AveragePricePerDay { get; set; }
        public double HighestPricePerDay { get; set; }
        public double LowestPricePerDay { get; set; }

        public int TotalBookings { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadCarReportAsync();
            return Page();
        }

        public async Task<IActionResult> OnGetDownloadPdfAsync()
        {
            await LoadCarReportAsync();

            /*
                IMPORTANT:
                QuestPDF requires license setup before generating the PDF.
                Community license is okay for school/small project use.
            */
            QuestPDF.Settings.License = LicenseType.Community;

            var pdfBytes = GenerateCarReportPdf();
            var fileName = $"car-report-{DateTime.Now:yyyyMMdd-HHmmss}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task LoadCarReportAsync()
        {
            /*
                IMPORTANT:
                Load all cars and bookings separately.

                This avoids depending on navigation properties inside the Car model.
            */
            var allCars = await _context.Cars
                .AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            var bookings = await _context.Bookings
                .AsNoTracking()
                .ToListAsync();

            Categories = allCars
                .Where(c => !string.IsNullOrWhiteSpace(c.Category))
                .Select(c => c.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            Transmissions = allCars
                .Where(c => !string.IsNullOrWhiteSpace(c.Transmission))
                .Select(c => c.Transmission)
                .Distinct()
                .OrderBy(t => t)
                .ToList();

            var rows = allCars.Select(car =>
            {
                var carBookings = bookings
                    .Where(b => b.CarId == car.CarId)
                    .ToList();

                return new AdminCarReportRow
                {
                    CarId = car.CarId,
                    Make = car.Make,
                    Model = car.Model,
                    Year = car.Year,
                    Category = car.Category,
                    PricePerDay = car.PricePerDay,
                    Status = car.Status,
                    Seats = car.Seats,
                    Transmission = car.Transmission,
                    Color = car.Color,
                    LicensePlate = car.LicensePlate,
                    VIN = car.VIN,
                    CreatedAt = car.CreatedAt,

                    TotalBookings = carBookings.Count,
                    ActiveBookings = carBookings.Count(b =>
                        b.Status == "approved" || b.Status == "started"),

                    CompletedBookings = carBookings.Count(b =>
                        b.Status == "completed"),

                    CancelledBookings = carBookings.Count(b =>
                        b.Status == "cancelled")
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.Trim().ToLower();

                rows = rows.Where(c =>
                    c.CarId.ToString().Contains(keyword) ||
                    c.Make.ToLower().Contains(keyword) ||
                    c.Model.ToLower().Contains(keyword) ||
                    c.Category.ToLower().Contains(keyword) ||
                    c.Status.ToLower().Contains(keyword) ||
                    c.Transmission.ToLower().Contains(keyword) ||
                    c.Color.ToLower().Contains(keyword) ||
                    c.LicensePlate.ToLower().Contains(keyword) ||
                    c.VIN.ToLower().Contains(keyword)
                ).ToList();
            }

            if (!string.IsNullOrWhiteSpace(StatusFilter))
            {
                rows = rows
                    .Where(c => c.Status.Equals(StatusFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(CategoryFilter))
            {
                rows = rows
                    .Where(c => c.Category.Equals(CategoryFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(TransmissionFilter))
            {
                rows = rows
                    .Where(c => c.Transmission.Equals(TransmissionFilter, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            Cars = rows;

            LoadStatistics(rows);
        }

        private void LoadStatistics(List<AdminCarReportRow> rows)
        {
            TotalCars = rows.Count;

            AvailableCars = rows.Count(c =>
                c.Status.Equals("available", StringComparison.OrdinalIgnoreCase));

            BookedCars = rows.Count(c =>
                c.Status.Equals("booked", StringComparison.OrdinalIgnoreCase));

            MaintenanceCars = rows.Count(c =>
                c.Status.Equals("maintenance", StringComparison.OrdinalIgnoreCase));

            InactiveCars = rows.Count(c =>
                c.Status.Equals("inactive", StringComparison.OrdinalIgnoreCase));

            TotalBookings = rows.Sum(c => c.TotalBookings);

            AveragePricePerDay = rows.Count == 0
                ? 0
                : rows.Average(c => c.PricePerDay);

            HighestPricePerDay = rows.Count == 0
                ? 0
                : rows.Max(c => c.PricePerDay);

            LowestPricePerDay = rows.Count == 0
                ? 0
                : rows.Min(c => c.PricePerDay);
        }

        private byte[] GenerateCarReportPdf()
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

                        header.Item().Text("Car Report")
                            .FontSize(14)
                            .SemiBold();

                        header.Item().Text($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(9);
                    });

                    page.Content().PaddingTop(15).Column(content =>
                    {
                        content.Item().Row(row =>
                        {
                            row.RelativeItem().Text($"Total Cars: {TotalCars}").Bold();
                            row.RelativeItem().Text($"Available: {AvailableCars}").Bold();
                            row.RelativeItem().Text($"Booked: {BookedCars}").Bold();
                            row.RelativeItem().Text($"Maintenance: {MaintenanceCars}").Bold();
                            row.RelativeItem().Text($"Inactive: {InactiveCars}").Bold();
                        });

                        content.Item().PaddingVertical(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Average Price/Day: PHP {AveragePricePerDay:N2}");
                            row.RelativeItem().Text($"Highest Price/Day: PHP {HighestPricePerDay:N2}");
                            row.RelativeItem().Text($"Lowest Price/Day: PHP {LowestPricePerDay:N2}");
                            row.RelativeItem().Text($"Total Bookings: {TotalBookings}");
                        });

                        content.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(40);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(45);
                                columns.ConstantColumn(55);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.ConstantColumn(70);
                                columns.ConstantColumn(55);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("ID");
                                header.Cell().Element(HeaderCell).Text("Vehicle");
                                header.Cell().Element(HeaderCell).Text("Category");
                                header.Cell().Element(HeaderCell).Text("Status");
                                header.Cell().Element(HeaderCell).Text("Seats");
                                header.Cell().Element(HeaderCell).Text("Year");
                                header.Cell().Element(HeaderCell).Text("Transmission");
                                header.Cell().Element(HeaderCell).Text("Plate No.");
                                header.Cell().Element(HeaderCell).Text("Price/Day");
                                header.Cell().Element(HeaderCell).Text("Bookings");
                            });

                            foreach (var car in Cars)
                            {
                                table.Cell().Element(BodyCell).Text(car.CarId.ToString());
                                table.Cell().Element(BodyCell).Text($"{car.Make} {car.Model}");
                                table.Cell().Element(BodyCell).Text(car.Category);
                                table.Cell().Element(BodyCell).Text(car.DisplayStatus);
                                table.Cell().Element(BodyCell).Text(car.Seats.ToString());
                                table.Cell().Element(BodyCell).Text(car.Year.ToString());
                                table.Cell().Element(BodyCell).Text(car.Transmission);
                                table.Cell().Element(BodyCell).Text(car.LicensePlate);
                                table.Cell().Element(BodyCell).Text($"PHP {car.PricePerDay:N2}");
                                table.Cell().Element(BodyCell).Text(car.TotalBookings.ToString());
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

    public class AdminCarReportRow
    {
        public int CarId { get; set; }

        public string Make { get; set; } = "";
        public string Model { get; set; } = "";

        public int Year { get; set; }

        public string Category { get; set; } = "";
        public double PricePerDay { get; set; }

        public string Status { get; set; } = "";
        public int Seats { get; set; }

        public string Transmission { get; set; } = "";
        public string Color { get; set; } = "";

        public string LicensePlate { get; set; } = "";
        public string VIN { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public int TotalBookings { get; set; }
        public int ActiveBookings { get; set; }
        public int CompletedBookings { get; set; }
        public int CancelledBookings { get; set; }

        public string VehicleName => $"{Make} {Model}";

        public string DisplayStatus =>
            string.IsNullOrWhiteSpace(Status)
                ? "Unknown"
                : Status.Replace("_", " ");
    }
}