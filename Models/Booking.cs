namespace VentureCarRentals.Models
{
    public class Booking
    {
        public int BookingId { get; set; }

        public int CarId { get; set; }
        public Car? Car { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public double TotalPrice { get; set; }

        public string Status { get; set; } = "pending";
        // pending, confirmed, cancelled, completed

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Payment? Payment { get; set; }
        public RentalAgreement? RentalAgreement { get; set; }
        public Review? Review { get; set; }
    }
}