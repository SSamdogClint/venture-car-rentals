namespace VentureCarRentals.Models
{
    public class MaintenanceLog
    {
        public int MaintenanceLogId { get; set; }

        public int CarId { get; set; }
        public Car? Car { get; set; }

        public string Description { get; set; } = "";

        public string MaintenanceStatus { get; set; } = "ongoing";
        // ongoing, completed

        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public double Cost { get; set; }
    }
}