using System;
using System.ComponentModel.DataAnnotations;

namespace VentureCarRentals.Models
{
    public class Car
    {
        public int CarId { get; set; }

        [Required]
        public string Make { get; set; } = "";

        [Required]
        public string Model { get; set; } = "";

        public int Year { get; set; }

        public string Category { get; set; } = "";

        public double PricePerDay { get; set; }

        public string Status { get; set; } = "available";

        public int Seats { get; set; }

        public string Transmission { get; set; } = "";

        public string Description { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        // Added for rental agreement and admin record purposes.
        // Do not display this on the public/user Browse Cars page.
        public string Color { get; set; } = "";

        // Added for rental agreement and admin vehicle tracking.
        // Keep hidden from normal users while browsing.
        public string LicensePlate { get; set; } = "";

        // Added for official rental agreement.
        // VIN should only appear in admin pages and generated contracts.
        public string VIN { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}