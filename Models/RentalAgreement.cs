namespace VentureCarRentals.Models
{
    public class RentalAgreement
    {
        public int RentalAgreementId { get; set; }

        public int BookingId { get; set; }
        public Booking? Booking { get; set; }

        public string FileUrl { get; set; } = "";

        public int SignedByUserId { get; set; }
        public User? SignedByUser { get; set; }

        public DateTime SignedAt { get; set; } = DateTime.Now;
    }
}