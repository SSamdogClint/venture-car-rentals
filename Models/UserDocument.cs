namespace VentureCarRentals.Models
{
    public class UserDocument
    {
        public int UserDocumentId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }

        public string DocType { get; set; } = "";
        public string DocNumber { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public string IssuingCountry { get; set; } = "";

        public DateTime? ExpiryDate { get; set; }

        public string Status { get; set; } = "pending";
        // pending, approved, rejected, expired

        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}