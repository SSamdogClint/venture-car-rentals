using System;
using System.Collections.Generic;

namespace VentureCarRentals.Models
{
    public class User
    {
        public int UserId { get; set; }

        public string FirstName { get; set; } = "";
        public string MiddleName { get; set; } = "";
        public string LastName { get; set; } = "";

        public string Email { get; set; } = "";
        public string PasswordHash { get; set; } = "";

        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }

        // Renter profile fields
        public string PhoneNumber { get; set; } = "";
        public string Street { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string City { get; set; } = "";
        public string State { get; set; } = "";
        public string ZipCode { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime? Birthday { get; set; }

        // Navigation properties
        public List<Booking> Bookings { get; set; } = new();
        public List<UserDocument> UserDocuments { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
    }
}