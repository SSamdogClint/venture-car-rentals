using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VentureCarRentals.Models;

namespace VentureCarRentals.Helpers
{
    public static class RentalAgreementContractGenerator
    {
        public static string GenerateBlankAgreementFile(
            string webRootPath,
            Booking booking,
            User renter,
            Car car,
            string agreementText,
            string driverLicenseNumber = "")
        {
            // Create folder inside wwwroot so admin can open or download the generated PDF.
            var folderPath = Path.Combine(webRootPath, "uploads", "rental-agreements", "generated");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            // Unique file name prevents overwriting old agreement files.
            var fileName = $"blank_agreement_booking_{booking.BookingId}_{Guid.NewGuid()}.pdf";
            var filePath = Path.Combine(folderPath, fileName);

            var renterName = $"{renter.FirstName} {renter.LastName}";
            var renterAddress = BuildRenterAddress(renter);
            var renterEmail = renter.Email;
            var renterPhone = string.IsNullOrWhiteSpace(renter.PhoneNumber) ? "N/A" : renter.PhoneNumber;

            var carName = $"{car.Make} {car.Model}";
            var carDetails = $"{car.Year} • {car.Category} • {car.Transmission} • {car.Seats} seats";

            var rentalFee = booking.TotalPrice * 0.75;
            var securityDeposit = booking.TotalPrice * 0.20;
            var serviceFee = booking.TotalPrice * 0.05;

            // Generates a real A4 PDF agreement document.
            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);

                    page.DefaultTextStyle(text =>
                        text.FontSize(10)
                            .FontFamily("Arial")
                            .FontColor(Colors.Grey.Darken4)
                    );

                    page.Header().Column(column =>
                    {
                        column.Item().AlignCenter().Text("VENTURE CAR RENTALS")
                            .Bold()
                            .FontSize(18)
                            .FontColor(Colors.Blue.Darken4);

                        column.Item().AlignCenter().Text("VEHICLE RENTAL AGREEMENT")
                            .SemiBold()
                            .FontSize(14);

                        column.Item().AlignCenter().Text($"Booking No. #{booking.BookingId}")
                            .FontSize(10)
                            .FontColor(Colors.Grey.Darken1);

                        column.Item().PaddingTop(8).LineHorizontal(1);
                    });

                    page.Content().PaddingTop(12).Column(column =>
                    {
                        column.Spacing(10);

                        AddSectionTitle(column, "1. THE PARTIES");

                        AddTwoColumnTable(column, new List<(string Label, string Value)>
                        {
                            ("Agreement Date", DateTime.Now.ToString("MMMM dd, yyyy")),
                            ("Company / Lessor", "Venture Car Rentals"),
                            ("Business Address", "Pahina Central, Sanciangko Street"),
                            ("Renter / Lessee", renterName),
                            ("Renter Address", renterAddress),
                            ("Renter Email", renterEmail),
                            ("Renter Contact No.", renterPhone),
                            ("Driver's License No.", string.IsNullOrWhiteSpace(driverLicenseNumber)
                                ? "[To be verified from submitted document]"
                                : driverLicenseNumber)
                        });

                        AddSectionTitle(column, "2. THE VEHICLE");

                        AddTwoColumnTable(column, new List<(string Label, string Value)>
                        {
                            ("Make / Model", carName),
                            ("Vehicle Details", carDetails),
                            ("License Plate", string.IsNullOrWhiteSpace(car.LicensePlate)
                                ? "[For admin record]"
                                : car.LicensePlate),
                            ("VIN", string.IsNullOrWhiteSpace(car.VIN)
                                ? "[For admin record]"
                                : car.VIN)
                        });

                        AddSectionTitle(column, "3. RENTAL TERM");

                        AddTwoColumnTable(column, new List<(string Label, string Value)>
                        {
                            ("Start Date / Time", booking.StartDate.ToString("MMMM dd, yyyy hh:mm tt")),
                            ("Return Date / Time", booking.EndDate.ToString("MMMM dd, yyyy hh:mm tt"))
                        });

                        column.Item().Text(
                            "The Renter shall have possession of the vehicle starting from the approved start date and time " +
                            "and must return it no later than the approved return date and time. Any unauthorized extension may result in late fees."
                        );

                        AddSectionTitle(column, "4. FEES AND DEPOSIT");

                        AddTwoColumnTable(column, new List<(string Label, string Value)>
                        {
                            ("Rental Fee", $"PHP {rentalFee:N2}"),
                            ("Refundable Security Deposit", $"PHP {securityDeposit:N2}"),
                            ("Service / Processing Fee", $"PHP {serviceFee:N2}"),
                            ("Total Amount", $"PHP {booking.TotalPrice:N2}")
                        });

                        column.Item().Text(
                            "The security deposit shall be refundable after the vehicle is returned, provided that there are no damages, " +
                            "missing fuel charges, late return penalties, traffic violations, or other unpaid obligations."
                        );

                        column.Item().Text(
                            "Fuel Policy: The vehicle must be returned with the same fuel level as when it was released. " +
                            "Any fuel shortage shall be charged to the renter."
                        );

                        AddSectionTitle(column, "5. TERMS OF USE");

                        AddBulletList(column, new[]
                        {
                            "Only the registered renter is authorized to drive the vehicle.",
                            "The vehicle shall not be used for illegal activities, racing, or reckless driving.",
                            "The vehicle shall not be subleased or rented to another person.",
                            "The vehicle shall not be used for towing or pushing other vehicles or objects.",
                            "The renter must follow all traffic laws and road safety regulations."
                        });

                        AddSectionTitle(column, "6. DAMAGE AND LIABILITY");

                        column.Item().Text(
                            "The Renter is responsible for traffic fines, toll fees, penalties, and damages incurred during the rental period. " +
                            "In case of accident, damage, theft, or emergency, the Renter must immediately notify the Company and provide " +
                            "the required incident report, police report, or supporting documents."
                        );

                        AddSectionTitle(column, "7. ONLINE AGREEMENT CONFIRMATION");

                        column.Item().Text(agreementText);

                        AddSectionTitle(column, "8. SIGNATURES");

                        column.Item().Text(
                            "By signing below, both parties confirm that they have read, understood, and agreed to the terms and conditions stated in this agreement."
                        );

                        column.Item().PaddingTop(45).Row(row =>
                        {
                            row.RelativeItem().Column(signature =>
                            {
                                signature.Item().LineHorizontal(1);
                                signature.Item().AlignCenter().Text("Company Representative Signature / Date").FontSize(9);
                            });

                            row.ConstantItem(40);

                            row.RelativeItem().Column(signature =>
                            {
                                signature.Item().LineHorizontal(1);
                                signature.Item().AlignCenter().Text("Renter Signature / Date").FontSize(9);
                            });
                        });
                    });

                    // Footer note for the generated blank agreement.
                    page.Footer()
                        .AlignCenter()
                        .Text("This blank agreement was generated by the system after the renter accepted the online agreement. Admin may print this file, have it signed face-to-face, then upload the signed copy.")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf(filePath);

            // Public URL that can be opened or downloaded by admin in the browser.
            return $"/uploads/rental-agreements/generated/{fileName}";
        }

        private static string BuildRenterAddress(User renter)
        {
            var parts = new[]
            {
                renter.Street,
                renter.Barangay,
                renter.City,
                renter.State,
                renter.ZipCode,
                renter.Country
            };

            var address = string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));

            return string.IsNullOrWhiteSpace(address) ? "[Customer Address]" : address;
        }

        private static void AddSectionTitle(ColumnDescriptor column, string title)
        {
            column.Item().PaddingTop(4).Text(title)
                .Bold()
                .FontSize(11)
                .FontColor(Colors.Blue.Darken4);

            column.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
        }

        private static void AddTwoColumnTable(ColumnDescriptor column, List<(string Label, string Value)> rows)
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(150);
                    columns.RelativeColumn();
                });

                foreach (var row in rows)
                {
                    table.Cell().Element(LabelCell).Text(row.Label).SemiBold();
                    table.Cell().Element(ValueCell).Text(row.Value);
                }
            });
        }

        private static IContainer LabelCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten1)
                .Background(Colors.Grey.Lighten4)
                .Padding(5);
        }

        private static IContainer ValueCell(IContainer container)
        {
            return container
                .Border(0.5f)
                .BorderColor(Colors.Grey.Lighten1)
                .Padding(5);
        }

        private static void AddBulletList(ColumnDescriptor column, string[] items)
        {
            foreach (var item in items)
            {
                column.Item().Row(row =>
                {
                    row.ConstantItem(12).Text("•");
                    row.RelativeItem().Text(item);
                });
            }
        }
    }
}