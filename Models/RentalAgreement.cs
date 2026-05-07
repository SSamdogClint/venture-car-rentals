using System;
using System.ComponentModel.DataAnnotations;

namespace VentureCarRentals.Models
{
    public class RentalAgreement
    {
        public int RentalAgreementId { get; set; }

        public int BookingId { get; set; }

        
        // Stores the online agreement text accepted by the user.
        // This is the digital acknowledgement before the booking is submitted.
        
        [Required]
        public string AgreementText { get; set; } = "";

        // online_accepted = user agreed online
        // signed_uploaded = admin uploaded signed physical agreement
        // approved = agreement reviewed and booking approved
        [Required]
        public string Status { get; set; } = "online_accepted";

        public DateTime? OnlineAcceptedAt { get; set; }

        // Admin uploads the face-to-face signed agreement here.
        // Example: /uploads/agreements/agreement_1.pdf
        
        public string? SignedAgreementFileUrl { get; set; }

        public DateTime? SignedUploadedAt { get; set; }

        public Booking? Booking { get; set; }
    }
}