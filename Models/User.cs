namespace VentureCarRentals.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        public bool IsAdmin { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<Booking> Bookings { get; set; } = new();
        public List<UserDocument> UserDocuments { get; set; } = new();
    }
}