using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

// This alias prevents conflict with the namespace VentureCarRentals.Pages.User.
using AppUser = VentureCarRentals.Models.User;

namespace VentureCarRentals.Pages.Admin.Documents
{
    public class VerificationModel : PageModel
    {
        private readonly AppDbContext _context;

        public VerificationModel(AppDbContext context)
        {
            _context = context;
        }

        public List<UserVerificationRowViewModel> Users { get; set; } = new();
        public List<DocumentRowViewModel> Documents { get; set; } = new();

        public int TotalUsers { get; set; }
        public int PendingUsers { get; set; }
        public int VerifiedUsers { get; set; }
        public int RejectedUsers { get; set; }
        public int ExpiredUsers { get; set; }

        public string SelectedUserFullName { get; set; } = "";
        public string SelectedUserEmail { get; set; } = "";
        public string SelectedUserVerificationStatus { get; set; } = "";

        public string UserListTitle { get; set; } = "User Verification Queue";
        public string UserListSubtitle { get; set; } = "Users waiting for document checking";
        public string SearchPlaceholder { get; set; } = "Search users";

        public bool IsSelectedUserMode => UserId != null;

        [BindProperty(SupportsGet = true)]
        public int? UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? UserStatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty]
        public int UserDocumentId { get; set; }

        [BindProperty]
        public string Status { get; set; } = "";

        public async Task<IActionResult> OnGetAsync()
        {
            UserStatusFilter = NormalizeUserStatusFilter(UserStatusFilter);

            if (UserId != null)
            {
                var selectedUserLoaded = await LoadSelectedUserDocumentsAsync(UserId.Value);

                if (!selectedUserLoaded)
                {
                    TempData["Error"] = "Selected user was not found.";
                    return RedirectToPage();
                }

                await LoadUserStatisticsAsync();

                SearchPlaceholder = "Search user documents";
                return Page();
            }

            await LoadUserVerificationQueueAsync();

            SearchPlaceholder = "Search users";

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            var document = await _context.UserDocuments.FindAsync(UserDocumentId);

            if (document == null)
            {
                TempData["Error"] = "Document not found.";
                return RedirectToPage(new { UserId, SearchTerm });
            }

            var allowedStatuses = new[] { "pending", "approved", "rejected" };

            Status = Status?.ToLower().Trim() ?? "";

            if (!allowedStatuses.Contains(Status))
            {
                TempData["Error"] = "Invalid document status.";
                return RedirectToPage(new { UserId, SearchTerm });
            }

            /*
                IMPORTANT FEATURE:
                Expired documents cannot be approved.
                Admin must request an updated document from the user.
            */
            if (Status == "approved" && IsExpired(document))
            {
                TempData["Error"] = "Expired documents cannot be approved.";
                return RedirectToPage(new { UserId, SearchTerm });
            }

            try
            {
                document.Status = Status;

                await _context.SaveChangesAsync();

                TempData["Success"] = "Document status updated successfully.";
                return RedirectToPage(new { UserId, SearchTerm });
            }
            catch
            {
                TempData["Error"] = "Something went wrong while updating the document status.";
                return RedirectToPage(new { UserId, SearchTerm });
            }
        }

        private async Task LoadUserStatisticsAsync()
        {
            var users = await _context.Users
                .Where(u => !u.IsAdmin)
                .ToListAsync();

            var documents = await _context.UserDocuments.ToListAsync();

            var userRows = BuildUserRows(users, documents);

            TotalUsers = userRows.Count;
            PendingUsers = userRows.Count(u => u.VerificationStatus == "Pending");
            VerifiedUsers = userRows.Count(u => u.VerificationStatus == "Verified");
            RejectedUsers = userRows.Count(u => u.VerificationStatus == "Rejected");
            ExpiredUsers = userRows.Count(u => u.VerificationStatus == "Expired");
        }

        private async Task LoadUserVerificationQueueAsync()
        {
            var users = await _context.Users
                .Where(u => !u.IsAdmin)
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            var documents = await _context.UserDocuments.ToListAsync();

            var allUserRows = BuildUserRows(users, documents);

            TotalUsers = allUserRows.Count;
            PendingUsers = allUserRows.Count(u => u.VerificationStatus == "Pending");
            VerifiedUsers = allUserRows.Count(u => u.VerificationStatus == "Verified");
            RejectedUsers = allUserRows.Count(u => u.VerificationStatus == "Rejected");
            ExpiredUsers = allUserRows.Count(u => u.VerificationStatus == "Expired");

            Users = allUserRows;

            if (!string.IsNullOrWhiteSpace(UserStatusFilter) && UserStatusFilter != "all")
            {
                Users = Users
                    .Where(u => u.VerificationStatus.ToLower() == UserStatusFilter)
                    .ToList();
            }

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

        private List<UserVerificationRowViewModel> BuildUserRows(
            List<AppUser> users,
            List<UserDocument> documents)
        {
            var rows = new List<UserVerificationRowViewModel>();

            foreach (var user in users)
            {
                var userDocuments = documents
                    .Where(d => d.UserId == user.UserId)
                    .ToList();

                var requiredInfo = GetRequiredDocumentInfo(user, userDocuments);
                var verificationStatus = GetVerificationStatus(user, requiredInfo);

                rows.Add(new UserVerificationRowViewModel
                {
                    UserId = user.UserId,
                    FullName = $"{user.FirstName} {user.LastName}",
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Country = user.Country,
                    CreatedAt = user.CreatedAt,
                    VerificationStatus = verificationStatus,
                    IsProfileComplete = IsProfileComplete(user),
                    DocumentCount = userDocuments.Count,
                    RequiredDocumentTotal = requiredInfo.RequiredTotal,
                    RequiredSubmittedCount = requiredInfo.SubmittedCount,
                    RequiredApprovedCount = requiredInfo.ApprovedCount,
                    RequiredDocuments = GetRequiredDocumentsText(user)
                });
            }

            return rows;
        }

        private async Task<bool> LoadSelectedUserDocumentsAsync(int selectedUserId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == selectedUserId && !u.IsAdmin);

            if (user == null)
            {
                return false;
            }

            var userDocumentsQuery = _context.UserDocuments
                .Where(d => d.UserId == selectedUserId);

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                userDocumentsQuery = userDocumentsQuery.Where(d =>
                    d.DocType.Contains(SearchTerm) ||
                    d.DocNumber.Contains(SearchTerm) ||
                    d.IssuingCountry.Contains(SearchTerm) ||
                    d.Status.Contains(SearchTerm));
            }

            var userDocuments = await userDocumentsQuery
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var requiredInfo = GetRequiredDocumentInfo(user, userDocuments);

            SelectedUserFullName = $"{user.FirstName} {user.LastName}";
            SelectedUserEmail = user.Email;
            SelectedUserVerificationStatus = GetVerificationStatus(user, requiredInfo);

            Documents = userDocuments.Select(document => new DocumentRowViewModel
            {
                UserDocumentId = document.UserDocumentId,
                UserId = document.UserId,
                UserFullName = SelectedUserFullName,
                UserEmail = SelectedUserEmail,
                DocType = document.DocType,
                DocNumber = document.DocNumber,
                FileUrl = document.FileUrl,
                IssuingCountry = document.IssuingCountry,
                ExpiryDate = document.ExpiryDate,
                Status = document.Status,
                UploadedAt = document.UploadedAt,
                IsExpired = IsExpired(document)
            }).ToList();

            return true;
        }

        private RequiredDocumentInfo GetRequiredDocumentInfo(AppUser user, List<UserDocument> documents)
        {
            /*
                IMPORTANT FEATURE:
                User is verified only when ALL required documents are approved.

                If only 1 required document is approved,
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
            if (!IsProfileComplete(user))
            {
                return "Incomplete";
            }

            if (!requiredInfo.HasAnySubmitted)
            {
                return "Incomplete";
            }

            if (requiredInfo.HasExpired)
            {
                return "Expired";
            }

            if (requiredInfo.HasRejected)
            {
                return "Rejected";
            }

            if (requiredInfo.SubmittedCount == requiredInfo.RequiredTotal &&
                requiredInfo.ApprovedCount == requiredInfo.RequiredTotal)
            {
                return "Verified";
            }

            return "Pending";
        }

        private bool IsProfileComplete(AppUser user)
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

        private bool IsExpired(UserDocument document)
        {
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
                "expired"
            };

            return allowedFilters.Contains(normalized) ? normalized : null;
        }

        private string GetRequiredDocumentsText(AppUser user)
        {
            var userCountry = user.Country?.ToLower() ?? "";

            if (userCountry != "philippines")
            {
                return "Passport + International Driving Permit";
            }

            return "Driver License + 1 Secondary ID";
        }

        private string GetUserListTitle(string? statusFilter)
        {
            return statusFilter switch
            {
                "pending" => "Pending Users",
                "verified" => "Verified Users",
                "rejected" => "Rejected Users",
                "expired" => "Expired Users",
                _ => "User Verification Queue"
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
                _ => "All users for document verification"
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

    public class UserVerificationRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Country { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public string VerificationStatus { get; set; } = "";
        public bool IsProfileComplete { get; set; }
        public int DocumentCount { get; set; }
        public int RequiredDocumentTotal { get; set; }
        public int RequiredSubmittedCount { get; set; }
        public int RequiredApprovedCount { get; set; }
        public string RequiredDocuments { get; set; } = "";
    }

    public class DocumentRowViewModel
    {
        public int UserDocumentId { get; set; }
        public int UserId { get; set; }
        public string UserFullName { get; set; } = "";
        public string UserEmail { get; set; } = "";
        public string DocType { get; set; } = "";
        public string DocNumber { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public string IssuingCountry { get; set; } = "";
        public DateTime? ExpiryDate { get; set; }
        public string Status { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public bool IsExpired { get; set; }
    }
}