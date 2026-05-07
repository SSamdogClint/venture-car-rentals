using System;

namespace VentureCarRentals.Models
{
    public class UserPaymentMethod
    {
        public int UserPaymentMethodId { get; set; }

        public int UserId { get; set; }

        public string PaymentType { get; set; } = "";
        // card, gcash, cash

        public string CardBrand { get; set; } = "";
        // visa, mastercard, jcb, amex

        public string CardHolderName { get; set; } = "";

        public string MaskedCardNumber { get; set; } = "";
        // example: **** **** **** 1234

        public string Last4 { get; set; } = "";

        public string ExpiryDate { get; set; } = "";
        // example: 12/31

        // active = card can be used
        // inactive = card is hidden/deactivated but still kept in database
        public string Status { get; set; } = "active";

        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }
    }
}