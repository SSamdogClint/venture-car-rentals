using System;

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

        // Expiry date of documents such as driver's license, passport,
        // and international driving permit.
        // Nullable because some secondary IDs may not have an expiry date.
        public DateTime? ExpiryDate { get; set; }

        // pending = waiting for admin review
        // approved = accepted by admin
        // rejected = rejected by admin
        // expired = expired document
        public string Status { get; set; } = "pending";

        // Date and time when the user uploaded or re-uploaded this document.
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}