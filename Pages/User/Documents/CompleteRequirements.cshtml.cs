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
            _context = context;
            _environment = environment;
        }

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

        [BindProperty]
        public string RenterType { get; set; } = "local";

        [BindProperty]
        public string DriverLicenseNumber { get; set; } = "";

        [BindProperty]
        public DateTime? DriverLicenseExpiry { get; set; }

        [BindProperty]
        public IFormFile? DriverLicenseFile { get; set; }

        [BindProperty]
        public string SecondaryDocType { get; set; } = "";

        [BindProperty]
        public string SecondaryDocNumber { get; set; } = "";

        [BindProperty]
        public IFormFile? SecondaryDocFile { get; set; }

        [BindProperty]
        public string PassportNumber { get; set; } = "";

        [BindProperty]
        public DateTime? PassportExpiry { get; set; }

        [BindProperty]
        public IFormFile? PassportFile { get; set; }

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
            Birthday = user.Birthday;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
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

            if (RenterType == "local")
            {
                if (string.IsNullOrWhiteSpace(DriverLicenseNumber) ||
                    DriverLicenseFile == null ||
                    string.IsNullOrWhiteSpace(SecondaryDocType) ||
                    string.IsNullOrWhiteSpace(SecondaryDocNumber) ||
                    SecondaryDocFile == null)
                {
                    TempData["Error"] = "Local renters must upload a driver’s license and one secondary ID.";
                    return Page();
                }
            }

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

            await _context.SaveChangesAsync();

            return RedirectToPage("/User/Bookings/Create", new
            {
                carId = CarId,
                borrowDate = BorrowDate,
                borrowTime = BorrowTime,
                returnDate = ReturnDate,
                returnTime = ReturnTime
            });
        }

        private async Task SaveDocumentAsync(
            int userId,
            string docType,
            string docNumber,
            IFormFile file,
            string issuingCountry,
            DateTime? expiryDate)
        {
            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "documents");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var extension = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"doc_{userId}_{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = await _context.UserDocuments
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DocType == docType);

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

            document.DocNumber = docNumber;
            document.FileUrl = $"/uploads/documents/{fileName}";
            document.IssuingCountry = issuingCountry;
            document.ExpiryDate = expiryDate;
            document.Status = "pending";
            document.UploadedAt = DateTime.Now;
        }
    }
}