namespace VentureCarRentals.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public string PaymentMethod { get; set; } = "";
        // cash, gcash, card

        public double Amount { get; set; }

        public string PaymentStatus { get; set; } = "unpaid";
        // unpaid, paid, refunded

        public DateTime? PaidAt { get; set; }
    }
}