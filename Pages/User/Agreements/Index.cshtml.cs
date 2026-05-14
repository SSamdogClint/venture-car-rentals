using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.User.Agreements
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<UserAgreementViewModel> Agreements { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            /*
                IMPORTANT:
                Load agreements together with Booking and Car.

                We use ToListAsync() first, then map to ViewModel after.
                This avoids nullable warning issues from direct Select().
            */
            var agreementRecords = await _context.RentalAgreements
                .Include(a => a.Booking!)
                .ThenInclude(b => b.Car)
                .Where(a => a.Booking != null && a.Booking.UserId == userId.Value)
                .OrderByDescending(a => a.GeneratedAt)
                .ToListAsync();

            Agreements = agreementRecords.Select(a =>
            {
                var booking = a.Booking;
                var car = booking?.Car;

                return new UserAgreementViewModel
                {
                    RentalAgreementId = a.RentalAgreementId,
                    BookingId = a.BookingId,

                    CarName = car == null
                        ? "Unknown Car"
                        : $"{car.Make} {car.Model}",

                    StartDate = booking?.StartDate ?? DateTime.MinValue,
                    EndDate = booking?.EndDate ?? DateTime.MinValue,
                    TotalPrice = booking?.TotalPrice ?? 0,

                    Status = a.Status ?? "",
                    DisplayStatus = GetDisplayStatus(a.Status),

                    OnlineAcceptedAt = a.OnlineAcceptedAt,
                    GeneratedAt = a.GeneratedAt,
                    SignedUploadedAt = a.SignedUploadedAt,

                    GeneratedAgreementFileUrl = a.GeneratedAgreementFileUrl,
                    SignedAgreementFileUrl = a.SignedAgreementFileUrl
                };
            }).ToList();

            return Page();
        }

        private static string GetDisplayStatus(string? status)
        {
            return status?.ToLower().Trim() switch
            {
                "online_accepted" => "Online Accepted",
                "signed_uploaded" => "Signed Uploaded",
                "approved" => "Approved",
                "cancelled" => "Cancelled",
                _ => "Pending"
            };
        }
    }

    public class UserAgreementViewModel
    {
        public int RentalAgreementId { get; set; }

        public int BookingId { get; set; }

        public string CarName { get; set; } = "";

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public double TotalPrice { get; set; }

        public string Status { get; set; } = "";

        public string DisplayStatus { get; set; } = "";

        public DateTime? OnlineAcceptedAt { get; set; }

        public DateTime? GeneratedAt { get; set; }

        public DateTime? SignedUploadedAt { get; set; }

        public string? GeneratedAgreementFileUrl { get; set; }

        public string? SignedAgreementFileUrl { get; set; }
    }
}