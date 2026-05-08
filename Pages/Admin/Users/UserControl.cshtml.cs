using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

// This alias prevents conflict with the namespace VentureCarRentals.Pages.User.
using AppUser = VentureCarRentals.Models.User;

namespace VentureCarRentals.Pages.Admin.Users
{
    public class UserControlModel : PageModel
    {
        private readonly AppDbContext _context;

        public UserControlModel(AppDbContext context)
        {
            _context = context;
        }

        // Users displayed in the table.
        public List<UserRowViewModel> Users { get; set; } = new();

        // Statistics cards.
        public int TotalUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int PendingUsers { get; set; }
        public int RejectedUsers { get; set; }
        public int ExpiredUsers { get; set; }
        public int IncompleteUsers { get; set; }

        // Right content title.
        public string UserListTitle { get; set; } = "Registered Users";
        public string UserListSubtitle { get; set; } = "All customer accounts";

        // Filter from statistics card View links.
        [BindProperty(SupportsGet = true)]
        public string? UserStatusFilter { get; set; }

        // Search keyword from search box.
        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        public async Task OnGetAsync()
        {
            // Normalize filter from URL.
            UserStatusFilter = NormalizeUserStatusFilter(UserStatusFilter);

            // Load all non-admin users.
            var users = await _context.Users
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            // Load all documents once to avoid repeated database calls.
            var documents = await _context.UserDocuments
                .ToListAsync();

            // Build user rows using the same verification rules as Document Verification.
            var allUserRows = BuildUserRows(users, documents);

            // Load statistics before filtering.
            TotalUsers = allUserRows.Count;
            VerifiedUsers = allUserRows.Count(u => u.VerificationStatus == "Verified");
            PendingUsers = allUserRows.Count(u => u.VerificationStatus == "Pending");
            RejectedUsers = allUserRows.Count(u => u.VerificationStatus == "Rejected");
            ExpiredUsers = allUserRows.Count(u => u.VerificationStatus == "Expired");
            IncompleteUsers = allUserRows.Count(u => u.VerificationStatus == "Incomplete");

            // Apply card filter.
            Users = allUserRows;

            if (!string.IsNullOrWhiteSpace(UserStatusFilter) && UserStatusFilter != "all")
            {
                Users = Users
                    .Where(u => u.VerificationStatus.ToLower() == UserStatusFilter)
                    .ToList();
            }

            // Apply search keyword.
            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                var keyword = SearchTerm.ToLower();

                Users = Users
                    .Where(u =>
                        u.FullName.ToLower().Contains(keyword) ||
                        u.Email.ToLower().Contains(keyword) ||
                        u.Country.ToLower().Contains(keyword) ||
                        u.VerificationStatus.ToLower().Contains(keyword))
                    .ToList();
            }

            UserListTitle = GetUserListTitle(UserStatusFilter);
            UserListSubtitle = GetUserListSubtitle(UserStatusFilter);
        }

        private List<UserRowViewModel> BuildUserRows(
            List<AppUser> users,
            List<UserDocument> documents)
        {
            var rows = new List<UserRowViewModel>();

            foreach (var user in users)
            {
                var userDocuments = documents
                    .Where(d => d.UserId == user.UserId)
                    .ToList();

                var requiredInfo = GetRequiredDocumentInfo(user, userDocuments);
                var verificationStatus = GetVerificationStatus(user, requiredInfo);

                rows.Add(new UserRowViewModel
                {
                    UserId = user.UserId,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Country = user.Country,
                    CreatedAt = user.CreatedAt,
                    VerificationStatus = verificationStatus,
                    DocumentCount = userDocuments.Count,
                    IsProfileComplete = IsProfileComplete(user),
                    RequiredDocumentTotal = requiredInfo.RequiredTotal,
                    RequiredSubmittedCount = requiredInfo.SubmittedCount,
                    RequiredApprovedCount = requiredInfo.ApprovedCount
                });
            }

            return rows;
        }

        private RequiredDocumentInfo GetRequiredDocumentInfo(AppUser user, List<UserDocument> documents)
        {
            /*
                IMPORTANT FEATURE:
                A user is verified only when ALL required documents are approved.

                If a user has 1 approved required document only,
                the user is still Pending.
            */

            var requiredDocuments = new List<UserDocument>();

            var userCountry = user.Country?.ToLower() ?? "";
            var isForeign = userCountry != "philippines";

            if (isForeign)
            {
                var passport = documents
                    .Where(d => d.DocType == "passport")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefault();

                var permit = documents
                    .Where(d => d.DocType == "international_driving_permit")
                    .OrderByDescending(d => d.UploadedAt)
                    .FirstOrDefault();

                if (passport != null)
                {
                    requiredDocuments.Add(passport);
                }

                if (permit != null)
                {
                    requiredDocuments.Add(permit);
                }

                return new RequiredDocumentInfo
                {
                    RequiredTotal = 2,
                    SubmittedCount = requiredDocuments.Count,
                    ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                    HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                    HasExpired = requiredDocuments.Any(IsExpired),
                    HasAnySubmitted = requiredDocuments.Any()
                };
            }

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

            var driverLicense = documents
                .Where(d => d.DocType == "driver_license")
                .OrderByDescending(d => d.UploadedAt)
                .FirstOrDefault();

            var secondaryId = documents
                .Where(d => secondaryTypes.Contains(d.DocType))
                .OrderByDescending(d => d.Status == "approved")
                .ThenByDescending(d => d.UploadedAt)
                .FirstOrDefault();

            if (driverLicense != null)
            {
                requiredDocuments.Add(driverLicense);
            }

            if (secondaryId != null)
            {
                requiredDocuments.Add(secondaryId);
            }

            return new RequiredDocumentInfo
            {
                RequiredTotal = 2,
                SubmittedCount = requiredDocuments.Count,
                ApprovedCount = requiredDocuments.Count(d => d.Status == "approved" && !IsExpired(d)),
                HasRejected = requiredDocuments.Any(d => d.Status == "rejected"),
                HasExpired = requiredDocuments.Any(IsExpired),
                HasAnySubmitted = requiredDocuments.Any()
            };
        }

        private string GetVerificationStatus(AppUser user, RequiredDocumentInfo requiredInfo)
        {
            // Profile must be complete first.
            if (!IsProfileComplete(user))
            {
                return "Incomplete";
            }

            // No required documents submitted yet.
            if (!requiredInfo.HasAnySubmitted)
            {
                return "Incomplete";
            }

            // Expired required document means user is expired.
            if (requiredInfo.HasExpired)
            {
                return "Expired";
            }

            // Rejected required document means user is rejected.
            if (requiredInfo.HasRejected)
            {
                return "Rejected";
            }

            // User becomes verified only if all required documents are submitted and approved.
            if (requiredInfo.SubmittedCount == requiredInfo.RequiredTotal &&
                requiredInfo.ApprovedCount == requiredInfo.RequiredTotal)
            {
                return "Verified";
            }

            // If user submitted/approved only part of the requirements, still pending.
            return "Pending";
        }

        private bool IsProfileComplete(AppUser user)
        {
            // User profile must be complete before verification can become valid.
            return !string.IsNullOrWhiteSpace(user.PhoneNumber) &&
                   !string.IsNullOrWhiteSpace(user.Street) &&
                   !string.IsNullOrWhiteSpace(user.Barangay) &&
                   !string.IsNullOrWhiteSpace(user.City) &&
                   !string.IsNullOrWhiteSpace(user.State) &&
                   !string.IsNullOrWhiteSpace(user.ZipCode) &&
                   !string.IsNullOrWhiteSpace(user.Country) &&
                   user.Birthday != null;
        }

        private bool IsExpired(UserDocument document)
        {
            // Checks if a document is past its expiry date.
            return document.ExpiryDate != null &&
                   document.ExpiryDate.Value.Date < DateTime.Today;
        }

        private string? NormalizeUserStatusFilter(string? statusFilter)
        {
            if (string.IsNullOrWhiteSpace(statusFilter))
            {
                return null;
            }

            var normalized = statusFilter.ToLower().Trim();

            var allowedFilters = new[]
            {
                "all",
                "pending",
                "verified",
                "rejected",
                "expired",
                "incomplete"
            };

            return allowedFilters.Contains(normalized) ? normalized : null;
        }

        private string GetUserListTitle(string? statusFilter)
        {
            return statusFilter switch
            {
                "pending" => "Pending Users",
                "verified" => "Verified Users",
                "rejected" => "Rejected Users",
                "expired" => "Expired Users",
                "incomplete" => "Incomplete Users",
                _ => "Registered Users"
            };
        }

        private string GetUserListSubtitle(string? statusFilter)
        {
            return statusFilter switch
            {
                "pending" => "Users with incomplete approval of required documents",
                "verified" => "Users with all required documents approved",
                "rejected" => "Users with rejected required documents",
                "expired" => "Users with expired required documents",
                "incomplete" => "Users missing profile or required document submission",
                _ => "All customer accounts"
            };
        }
    }

    public class RequiredDocumentInfo
    {
        public int RequiredTotal { get; set; }
        public int SubmittedCount { get; set; }
        public int ApprovedCount { get; set; }
        public bool HasRejected { get; set; }
        public bool HasExpired { get; set; }
        public bool HasAnySubmitted { get; set; }
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
        public int RequiredDocumentTotal { get; set; }
        public int RequiredSubmittedCount { get; set; }
        public int RequiredApprovedCount { get; set; }
    }
}