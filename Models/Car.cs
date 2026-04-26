namespace VentureCarRentals.Models
{
    public class Car
    {
        public int CarId { get; set; }

        public string Make { get; set; } = "";
        public string Model { get; set; } = "";
        public int Year { get; set; }
        public string Category { get; set; } = "";

        public double PricePerDay { get; set; }

        public string Status { get; set; } = "available";
        // available, booked, maintenance, inactive

        public int Seats { get; set; }
        public string Transmission { get; set; } = "";
        public string Description { get; set; } = "";
        public string ImageUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<Booking> Bookings { get; set; } = new();
        public List<MaintenanceLog> MaintenanceLogs { get; set; } = new();
    }
}