using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VentureCarRentals.Models
{
    public class Notification
    {
        public int NotificationId { get; set; }

        /*
            IMPORTANT:
            RecipientType separates admin and user notifications.

            admin = notification for admin
            user  = notification for specific user
        */
        [Required]
        public string RecipientType { get; set; } = "admin";

        /*
            IMPORTANT:
            Nullable foreign key.

            Admin notification:
                RecipientType = admin
                UserId = null

            User notification:
                RecipientType = user
                UserId = actual UserId
        */
        public int? UserId { get; set; }

        public User? User { get; set; }

        [Required]
        public string Title { get; set; } = "";

        public string Message { get; set; } = "";

        public string Type { get; set; } = "system";
        // booking, document, penalty, maintenance, review, system

        public string TargetUrl { get; set; } = "#";

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}