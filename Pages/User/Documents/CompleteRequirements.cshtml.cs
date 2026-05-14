using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Documents
{
    public class CompleteRequirementsModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public CompleteRequirementsModel(AppDbContext context, IWebHostEnvironment environment)
        {
            // Database context for Users, UserDocuments, and Notifications.
            _context = context;

            // Used to save uploaded document files inside wwwroot/uploads/documents.
            _environment = environment;
        }

        // These route values preserve the selected booking schedule if the user came from booking flow.
        [BindProperty(SupportsGet = true)]
        public int CarId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? BorrowTime { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnTime { get; set; }

        // Renter personal information.
        [BindProperty]
        public string FirstName { get; set; } = "";

        [BindProperty]
        public string MiddleName { get; set; } = "";

        [BindProperty]
        public string LastName { get; set; } = "";

        [BindProperty]
        public string Email { get; set; } = "";

        [BindProperty]
        public string PhoneNumber { get; set; } = "";

        [BindProperty]
        public string Street { get; set; } = "";

        [BindProperty]
        public string Barangay { get; set; } = "";

        [BindProperty]
        public string City { get; set; } = "";

        [BindProperty]
        public string State { get; set; } = "";

        [BindProperty]
        public string ZipCode { get; set; } = "";

        [BindProperty]
        public string Country { get; set; } = "";

        [BindProperty]
        public DateTime? Birthday { get; set; }

        // local or foreign renter.
        [BindProperty]
        public string RenterType { get; set; } = "local";

        // Local renter: driver's license.
        [BindProperty]
        public string DriverLicenseNumber { get; set; } = "";

        [BindProperty]
        public DateTime? DriverLicenseExpiry { get; set; }

        [BindProperty]
        public IFormFile? DriverLicenseFile { get; set; }

        // Local renter: one secondary ID.
        [BindProperty]
        public string SecondaryDocType { get; set; } = "";

        [BindProperty]
        public string SecondaryDocNumber { get; set; } = "";

        [BindProperty]
        public IFormFile? SecondaryDocFile { get; set; }

        // Foreign renter: passport.
        [BindProperty]
        public string PassportNumber { get; set; } = "";

        [BindProperty]
        public DateTime? PassportExpiry { get; set; }

        [BindProperty]
        public IFormFile? PassportFile { get; set; }

        // Foreign renter: international driving permit.
        [BindProperty]
        public string InternationalPermitNumber { get; set; } = "";

        [BindProperty]
        public DateTime? InternationalPermitExpiry { get; set; }

        [BindProperty]
        public IFormFile? InternationalPermitFile { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            // Pre-fill form using saved user profile information.
            FirstName = user.FirstName;
            LastName = user.LastName;
            Email = user.Email;

            MiddleName = user.MiddleName;
            PhoneNumber = user.PhoneNumber;
            Street = user.Street;
            Barangay = user.Barangay;
            City = user.City;
            State = user.State;
            ZipCode = user.ZipCode;
            Country = string.IsNullOrWhiteSpace(user.Country) ? "Philippines" : user.Country;

            // Default birthday shows 18 years old if no birthday is saved yet.
            Birthday = user.Birthday ?? DateTime.Today.AddYears(-18);

            // Default expiry dates so the input will not show only mm/dd/yyyy.
            DriverLicenseExpiry = DateTime.Today.AddYears(5);
            PassportExpiry = DateTime.Today.AddYears(5);
            InternationalPermitExpiry = DateTime.Today.AddYears(5);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            // User must be logged in before submitting verification requirements.
            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var user = await _context.Users.FindAsync(userId.Value);

            if (user == null)
            {
                return RedirectToPage("/Login");
            }

            /*
                IMPORTANT:
                This checks if the user already uploaded documents before this submission.

                If true:
                    Admin notification title becomes "Updated Verification Documents".

                If false:
                    Admin notification title becomes "New Verification Request".
            */
            var hadDocumentsBeforeSubmit = await _context.UserDocuments
                .AnyAsync(d => d.UserId == user.UserId);

            // Validate required renter profile fields.
            if (string.IsNullOrWhiteSpace(FirstName) ||
                string.IsNullOrWhiteSpace(LastName) ||
                string.IsNullOrWhiteSpace(PhoneNumber) ||
                string.IsNullOrWhiteSpace(Street) ||
                string.IsNullOrWhiteSpace(Barangay) ||
                string.IsNullOrWhiteSpace(City) ||
                string.IsNullOrWhiteSpace(State) ||
                string.IsNullOrWhiteSpace(ZipCode) ||
                string.IsNullOrWhiteSpace(Country) ||
                Birthday == null)
            {
                TempData["Error"] = "Please complete all renter information.";
                return Page();
            }

            // Validate required documents for local renters.
            if (RenterType == "local")
            {
                if (string.IsNullOrWhiteSpace(DriverLicenseNumber) ||
                    DriverLicenseFile == null ||
                    string.IsNullOrWhiteSpace(SecondaryDocType) ||
                    string.IsNullOrWhiteSpace(SecondaryDocNumber) ||
                    SecondaryDocFile == null)
                {
                    TempData["Error"] = "Local renters must upload a driver's license and one secondary ID.";
                    return Page();
                }
            }

            // Validate required documents for foreign renters.
            if (RenterType == "foreign")
            {
                if (string.IsNullOrWhiteSpace(PassportNumber) ||
                    PassportFile == null ||
                    string.IsNullOrWhiteSpace(InternationalPermitNumber) ||
                    InternationalPermitFile == null)
                {
                    TempData["Error"] = "Foreign renters must upload a passport and international driving permit/license.";
                    return Page();
                }
            }

            /*
                IMPORTANT:
                Update the user profile first.

                This makes sure the admin can see the latest renter information
                when reviewing the uploaded documents.
            */
            user.FirstName = FirstName;
            user.MiddleName = MiddleName ?? "";
            user.LastName = LastName;
            user.PhoneNumber = PhoneNumber;
            user.Street = Street;
            user.Barangay = Barangay;
            user.City = City;
            user.State = State;
            user.ZipCode = ZipCode;
            user.Country = Country;
            user.Birthday = Birthday;

            /*
                IMPORTANT:
                Save local renter documents.

                Local renters must provide:
                1. Driver's License
                2. One Secondary ID
            */
            if (RenterType == "local")
            {
                await SaveDocumentAsync(
                    user.UserId,
                    "driver_license",
                    DriverLicenseNumber,
                    DriverLicenseFile!,
                    Country,
                    DriverLicenseExpiry
                );

                await SaveDocumentAsync(
                    user.UserId,
                    SecondaryDocType,
                    SecondaryDocNumber,
                    SecondaryDocFile!,
                    Country,
                    null
                );
            }

            /*
                IMPORTANT:
                Save foreign renter documents.

                Foreign renters must provide:
                1. Passport
                2. International Driving Permit
            */
            if (RenterType == "foreign")
            {
                await SaveDocumentAsync(
                    user.UserId,
                    "passport",
                    PassportNumber,
                    PassportFile!,
                    Country,
                    PassportExpiry
                );

                await SaveDocumentAsync(
                    user.UserId,
                    "international_driving_permit",
                    InternationalPermitNumber,
                    InternationalPermitFile!,
                    Country,
                    InternationalPermitExpiry
                );
            }

            /*
                IMPORTANT:
                ADMIN NOTIFICATION WHEN USER SUBMITS VERIFICATION DOCUMENTS

                This creates a notification for the admin bell/dropdown.

                RecipientType = "admin"
                    Means this notification is for the admin side.

                UserId = null
                    Means it is not owned by one customer account.

                TargetUrl
                    Opens the admin verification page directly for this selected user.
            */
            _context.Notifications.Add(new Notification
            {
                RecipientType = "admin",
                UserId = null,
                Title = hadDocumentsBeforeSubmit
                    ? "Updated Verification Documents"
                    : "New Verification Request",
                Message = $"{user.FirstName} {user.LastName} submitted verification documents for admin review.",
                Type = "document",
                TargetUrl = $"/Admin/Documents/Verification?UserId={user.UserId}",
                IsRead = false,
                CreatedAt = DateTime.Now
            });

            /*
                IMPORTANT:
                SaveChangesAsync saves:
                - Updated user profile
                - Uploaded document records
                - Admin notification record
            */
            await _context.SaveChangesAsync();

            TempData["VerificationSubmitted"] =
                "Thank you for submitting your verification requirements. Please wait for 30 minutes to 1 day while the admin reviews and confirms your account verification.";

            return RedirectToPage("/User/Home");
        }

        private async Task SaveDocumentAsync(
            int userId,
            string docType,
            string docNumber,
            IFormFile file,
            string issuingCountry,
            DateTime? expiryDate)
        {
            /*
                IMPORTANT:
                This method saves the uploaded file to:

                wwwroot/uploads/documents

                Then it creates or updates the UserDocument database record.
            */

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "documents");

            // Create folder if it does not exist yet.
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Get file extension like .jpg, .png, .pdf.
            var extension = Path.GetExtension(file.FileName).ToLower();

            // Create unique file name to prevent duplicate file name conflict.
            var fileName = $"doc_{userId}_{Guid.NewGuid()}{extension}";

            // Final physical file path.
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Save uploaded file to the server.
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Check if the user already uploaded the same document type before.
            var document = await _context.UserDocuments
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DocType == docType);

            // If document does not exist, create a new one.
            if (document == null)
            {
                document = new UserDocument
                {
                    UserId = userId,
                    DocType = docType,
                    UploadedAt = DateTime.Now
                };

                _context.UserDocuments.Add(document);
            }

            /*
                IMPORTANT:
                Every new submission resets the document status to pending.

                This means admin must review it again.
            */
            document.DocNumber = docNumber;
            document.FileUrl = $"/uploads/documents/{fileName}";
            document.IssuingCountry = issuingCountry;
            document.ExpiryDate = expiryDate;
            document.Status = "pending";
            document.UploadedAt = DateTime.Now;
        }
    }
}
