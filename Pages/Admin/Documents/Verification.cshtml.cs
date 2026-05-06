using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.Admin.Documents
{
    public class VerificationModel : PageModel
    {
        private readonly AppDbContext _context;

        public VerificationModel(AppDbContext context)
        {
            _context = context;
        }

        public List<DocumentRowViewModel> Documents { get; set; } = new();

        public int TotalDocuments { get; set; }
        public int PendingDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int RejectedDocuments { get; set; }
        public int ExpiredDocuments { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? UserId { get; set; }

        [BindProperty]
        public int UserDocumentId { get; set; }

        [BindProperty]
        public string Status { get; set; } = "";

        public async Task OnGetAsync()
        {
            await LoadDocumentsAsync();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            var document = await _context.UserDocuments.FindAsync(UserDocumentId);

            if (document == null)
            {
                TempData["Error"] = "Document not found.";
                return RedirectToPage(new { UserId });
            }

            var allowedStatuses = new[] { "pending", "approved", "rejected" };

            Status = Status.ToLower();

            if (!allowedStatuses.Contains(Status))
            {
                TempData["Error"] = "Invalid document status.";
                return RedirectToPage(new { UserId });
            }

            document.Status = Status;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document status updated successfully.";

            return RedirectToPage(new { UserId });
        }

        private async Task LoadDocumentsAsync()
        {
            var users = await _context.Users.ToListAsync();

            var query = _context.UserDocuments.AsQueryable();

            if (UserId != null)
            {
                query = query.Where(d => d.UserId == UserId.Value);
            }

            var documents = await query
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            Documents = documents.Select(document =>
            {
                var user = users.FirstOrDefault(u => u.UserId == document.UserId);

                return new DocumentRowViewModel
                {
                    UserDocumentId = document.UserDocumentId,
                    UserId = document.UserId,
                    UserFullName = user == null ? "Unknown User" : $"{user.FirstName} {user.LastName}",
                    UserEmail = user == null ? "" : user.Email,
                    DocType = document.DocType,
                    DocNumber = document.DocNumber,
                    FileUrl = document.FileUrl,
                    IssuingCountry = document.IssuingCountry,
                    ExpiryDate = document.ExpiryDate,
                    Status = document.Status,
                    UploadedAt = document.UploadedAt
                };
            }).ToList();

            TotalDocuments = Documents.Count;
            PendingDocuments = Documents.Count(d => d.Status == "pending");
            ApprovedDocuments = Documents.Count(d => d.Status == "approved");
            RejectedDocuments = Documents.Count(d => d.Status == "rejected");
            ExpiredDocuments = Documents.Count(d => d.ExpiryDate != null && d.ExpiryDate < DateTime.Today);
        }
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
    }
}