using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VentureCarRentals.Models
{
    public class Penalty
    {
        [Key]
        public int PenaltyId { get; set; }

        public int BookingId { get; set; }

        public int OverdueHours { get; set; }

        public double RatePerHour { get; set; } = 200;

        public double Amount { get; set; }

        public string Status { get; set; } = "unpaid";
        // unpaid, paid, waived

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? PaidAt { get; set; }

        [ForeignKey("BookingId")]
        public Booking? Booking { get; set; }
    }
}