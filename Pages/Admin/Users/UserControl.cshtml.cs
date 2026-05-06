using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Users
{
    public class UserControlModel : PageModel
    {
        private readonly AppDbContext _context;

        public UserControlModel(AppDbContext context)
        {
            _context = context;
        }

        public List<UserRowViewModel> Users { get; set; } = new();

        public int TotalUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int PendingUsers { get; set; }
        public int IncompleteUsers { get; set; }

        public async Task OnGetAsync()
        {
            var users = await _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var documents = await _context.UserDocuments
                .ToListAsync();

            foreach (var user in users)
            {
                if (user.IsAdmin)
                {
                    continue;
                }

                var userDocs = documents
                    .Where(d => d.UserId == user.UserId)
                    .ToList();

                var status = GetVerificationStatus(user, userDocs);

                Users.Add(new UserRowViewModel
                {
                    UserId = user.UserId,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Country = user.Country,
                    CreatedAt = user.CreatedAt,
                    VerificationStatus = status,
                    DocumentCount = userDocs.Count,
                    IsProfileComplete = IsProfileComplete(user)
                });
            }

            TotalUsers = Users.Count;
            VerifiedUsers = Users.Count(u => u.VerificationStatus == "Verified");
            PendingUsers = Users.Count(u => u.VerificationStatus == "Pending");
            IncompleteUsers = Users.Count(u => u.VerificationStatus == "Incomplete");
        }

        private bool IsProfileComplete(Models.User user)
        {
            return !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(user.Street) &&
                   !string.IsNullOrWhiteSpace(user.Barangay) &&
                   !string.IsNullOrWhiteSpace(user.City) &&
                   !string.IsNullOrWhiteSpace(user.State) &&
                   !string.IsNullOrWhiteSpace(user.ZipCode) &&
                   !string.IsNullOrWhiteSpace(user.Country) &&
                   user.Birthday != null;
        }

        private string GetVerificationStatus(Models.User user, List<Models.UserDocument> documents)
        {
            if (!IsProfileComplete(user))
            {
                return "Incomplete";
            }

            var isForeign = user.Country.ToLower() != "philippines";

            if (isForeign)
            {
                var passport = documents.FirstOrDefault(d => d.DocType == "passport");
                var permit = documents.FirstOrDefault(d => d.DocType == "international_driving_permit");

                if (passport == null || permit == null)
                {
                    return "Incomplete";
                }

                if (passport.Status == "rejected" || permit.Status == "rejected")
                {
                    return "Rejected";
                }

                if (passport.Status == "approved" && permit.Status == "approved")
                {
                    return "Verified";
                }

                return "Pending";
            }
            else
            {
                var secondaryTypes = new[]
                {
                    "national_id",
                    "police_clearance",
                    "nbi_clearance",
                    "philhealth_id",
                    "sss_id",
                    "umid",
                    "voters_id",
                    "company_id"
                };

                var driverLicense = documents.FirstOrDefault(d => d.DocType == "driver_license");
                var secondaryId = documents.FirstOrDefault(d => secondaryTypes.Contains(d.DocType));

                if (driverLicense == null || secondaryId == null)
                {
                    return "Incomplete";
                }

                if (driverLicense.Status == "rejected" || secondaryId.Status == "rejected")
                {
                    return "Rejected";
                }

                if (driverLicense.Status == "approved" && secondaryId.Status == "approved")
                {
                    return "Verified";
                }

                return "Pending";
            }
        }
    }

    public class UserRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string VerificationStatus { get; set; } = "";
        public int DocumentCount { get; set; }
        public bool IsProfileComplete { get; set; }
    }
}