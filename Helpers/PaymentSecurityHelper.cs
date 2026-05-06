using System.Globalization;

namespace VentureCarRentals.Helpers
{
    public static class PaymentSecurityHelper
    {
        /*
            This helper file separates payment validation and security logic
            from the Razor Page model.

            Important security rule:
            The system must never save the full 16-digit card number.
            It only saves:
            - Card type
            - Masked card number
            - Last 4 digits
            - Card holder name
            - Expiry date
        */

        public static PaymentValidationResult ValidateDemoCard(
            string cardAccountNumber,
            string cardHolderName,
            string expiryDate)
        {
            var cleanCardNumber = CleanCardNumber(cardAccountNumber);
            var cleanHolderName = cardHolderName?.Trim() ?? "";
            var cleanExpiryDate = expiryDate?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(cleanCardNumber))
            {
                return PaymentValidationResult.Fail("Please enter a demo card number.");
            }

            if (cleanCardNumber.Length != 16)
            {
                return PaymentValidationResult.Fail("Demo card number must contain exactly 16 digits.");
            }

            if (!cleanCardNumber.All(char.IsDigit))
            {
                return PaymentValidationResult.Fail("Demo card number must contain numbers only.");
            }

            var cardIdentifier = cleanCardNumber.Substring(0, 4);
            var cardType = DetectCardType(cardIdentifier);

            if (cardType == "Unknown")
            {
                return PaymentValidationResult.Fail("Invalid demo card number. Start with 1234 for MasterCard, 5678 for Visa, or 4321 for JCB.");
            }

            if (string.IsNullOrWhiteSpace(cleanHolderName))
            {
                return PaymentValidationResult.Fail("Please enter the card holder name.");
            }

            if (cleanHolderName.Length < 3)
            {
                return PaymentValidationResult.Fail("Card holder name must be at least 3 characters.");
            }

            if (!IsValidExpiryDate(cleanExpiryDate))
            {
                return PaymentValidationResult.Fail("Please enter a valid future expiry date using MM/YY format.");
            }

            var last4 = cleanCardNumber.Substring(12, 4);
            var maskedNumber = MaskCardNumber(cleanCardNumber);

            return PaymentValidationResult.Success(
                cardType,
                maskedNumber,
                last4,
                cleanHolderName,
                cleanExpiryDate
            );
        }

        public static string CleanCardNumber(string cardAccountNumber)
        {
            if (string.IsNullOrWhiteSpace(cardAccountNumber))
            {
                return "";
            }

            return new string(cardAccountNumber.Where(char.IsDigit).ToArray());
        }

        public static string DetectCardType(string cardIdentifier)
        {
            return cardIdentifier switch
            {
                "1234" => "MasterCard",
                "5678" => "Visa",
                "4321" => "JCB",
                _ => "Unknown"
            };
        }

        public static string MaskCardNumber(string cleanCardNumber)
        {
            if (string.IsNullOrWhiteSpace(cleanCardNumber) || cleanCardNumber.Length != 16)
            {
                return "**** **** **** ****";
            }

            var first4 = cleanCardNumber.Substring(0, 4);
            var last4 = cleanCardNumber.Substring(12, 4);

            return $"{first4} **** **** {last4}";
        }

        public static bool IsValidExpiryDate(string expiryDate)
        {
            if (string.IsNullOrWhiteSpace(expiryDate))
            {
                return false;
            }

            if (!DateTime.TryParseExact(
                    expiryDate,
                    "MM/yy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return false;
            }

            var expiryEndOfMonth = new DateTime(
                parsedDate.Year,
                parsedDate.Month,
                DateTime.DaysInMonth(parsedDate.Year, parsedDate.Month)
            );

            return expiryEndOfMonth >= DateTime.Today;
        }
    }

    public class PaymentValidationResult
    {
        public bool IsValid { get; set; }

        public string ErrorMessage { get; set; } = "";

        public string CardType { get; set; } = "";

        public string MaskedCardNumber { get; set; } = "";

        public string Last4 { get; set; } = "";

        public string CardHolderName { get; set; } = "";

        public string ExpiryDate { get; set; } = "";

        public static PaymentValidationResult Fail(string errorMessage)
        {
            return new PaymentValidationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }

        public static PaymentValidationResult Success(
            string cardType,
            string maskedCardNumber,
            string last4,
            string cardHolderName,
            string expiryDate)
        {
            return new PaymentValidationResult
            {
                IsValid = true,
                CardType = cardType,
                MaskedCardNumber = maskedCardNumber,
                Last4 = last4,
                CardHolderName = cardHolderName,
                ExpiryDate = expiryDate
            };
        }
    }
}