using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Helpers;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Profile
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public IndexModel(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // These values are preserved when the user came from Browse Cars verification flow.
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

        /*
            IMPORTANT:
            These profile fields are nullable string? on purpose.

            If these are declared as non-nullable string, ASP.NET Core may automatically
            treat them as required and show errors like:
            "The Street field is required."
            even when the user selected Foreign.

            We manually validate fields based on RenterType.
        */

        [BindProperty]
        public string? FirstName { get; set; }

        [BindProperty]
        public string? MiddleName { get; set; }

        [BindProperty]
        public string? LastName { get; set; }

        [BindProperty]
        public string? Email { get; set; }

        [BindProperty]
        public string? PhoneNumber { get; set; }

        [BindProperty]
        public string? Street { get; set; }

        [BindProperty]
        public string? Barangay { get; set; }

        [BindProperty]
        public string? City { get; set; }

        [BindProperty]
        public string? State { get; set; }

        [BindProperty]
        public string? ZipCode { get; set; }

        [BindProperty]
        public string? Country { get; set; }

        [BindProperty]
        public DateTime? Birthday { get; set; }

        /*
            IMPORTANT FEATURE:
            RenterType controls the profile and document requirements.

            local:
            - Requires full local address.
            - Requires Driver's License + one Secondary ID.

            foreign:
            - Requires Country, City, and Zip Code only.
            - Requires Passport + International Driving Permit.
        */
        [BindProperty]
        public string? RenterType { get; set; } = "local";

        public DateTime CreatedAt { get; set; }

        // Local renter document fields.
        [BindProperty]
        public string? DriverLicenseNumber { get; set; }

        [BindProperty]
        public DateTime? DriverLicenseExpiry { get; set; }

        [BindProperty]
        public IFormFile? DriverLicenseFile { get; set; }

        [BindProperty]
        public string? SecondaryDocType { get; set; }

        [BindProperty]
        public string? SecondaryDocNumber { get; set; }

        [BindProperty]
        public IFormFile? SecondaryDocFile { get; set; }

        // Foreign renter document fields.
        [BindProperty]
        public string? PassportNumber { get; set; }

        [BindProperty]
        public DateTime? PassportExpiry { get; set; }

        [BindProperty]
        public IFormFile? PassportFile { get; set; }

        [BindProperty]
        public string? InternationalPermitNumber { get; set; }

        [BindProperty]
        public DateTime? InternationalPermitExpiry { get; set; }

        [BindProperty]
        public IFormFile? InternationalPermitFile { get; set; }

        // Existing uploaded document display values.
        public string ExistingDriverLicenseFileUrl { get; set; } = "";
        public string ExistingDriverLicenseStatus { get; set; } = "";

        public string ExistingSecondaryFileUrl { get; set; } = "";
        public string ExistingSecondaryStatus { get; set; } = "";

        public string ExistingPassportFileUrl { get; set; } = "";
        public string ExistingPassportStatus { get; set; } = "";

        public string ExistingInternationalPermitFileUrl { get; set; } = "";
        public string ExistingInternationalPermitStatus { get; set; } = "";

        public bool HasDriverLicense => !string.IsNullOrWhiteSpace(ExistingDriverLicenseFileUrl);
        public bool HasSecondaryId => !string.IsNullOrWhiteSpace(ExistingSecondaryFileUrl);
        public bool HasPassport => !string.IsNullOrWhiteSpace(ExistingPassportFileUrl);
        public bool HasInternationalPermit => !string.IsNullOrWhiteSpace(ExistingInternationalPermitFileUrl);

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

            // Load saved profile information.
            LoadUserToPage(user);

            // Load existing uploaded documents and fill document input fields.
            await LoadExistingDocumentsAsync(user.UserId, fillInputs: true);

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

            // Keep CreatedAt visible on the profile card if validation fails.
            CreatedAt = user.CreatedAt;

            NormalizeInputs();

            // Load existing uploaded document information before validating files.
            await LoadExistingDocumentsAsync(user.UserId, fillInputs: false);

            /*
                IMPORTANT:
                One Save Changes button validates both:
                1. Profile information
                2. Required documents based on selected RenterType

                The validation changes depending on:
                - local
                - foreign
            */
            ValidateProfileFields();
            ValidateDocumentFields();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var emailValue = Email ?? "";

            var emailExists = await _context.Users
                .AnyAsync(u => u.Email == emailValue && u.UserId != user.UserId);

            if (emailExists)
            {
                ModelState.AddModelError(nameof(Email), "Email is already used by another account.");
                return Page();
            }

            var hadDocumentsBeforeSubmit = await _context.UserDocuments
                .AnyAsync(d => d.UserId == user.UserId);

            try
            {
                /*
                    IMPORTANT:
                    Save latest profile data first.

                    The admin verification page will use this saved profile information
                    when reviewing renter documents.
                */
                UpdateUserProfile(user);

                var documentChanged = false;

                if (RenterType == "local")
                {
                    /*
                        IMPORTANT:
                        Local renters must submit:
                        - Driver's License
                        - One Secondary ID
                    */
                    documentChanged |= await SaveOrUpdateDocumentAsync(
                        user.UserId,
                        "driver_license",
                        DriverLicenseNumber ?? "",
                        DriverLicenseFile,
                        Country ?? "",
                        DriverLicenseExpiry
                    );

                    documentChanged |= await SaveOrUpdateDocumentAsync(
                        user.UserId,
                        SecondaryDocType ?? "",
                        SecondaryDocNumber ?? "",
                        SecondaryDocFile,
                        Country ?? "",
                        null
                    );
                }
                else
                {
                    /*
                        IMPORTANT:
                        Foreign renters must submit:
                        - Passport
                        - International Driving Permit
                    */
                    documentChanged |= await SaveOrUpdateDocumentAsync(
                        user.UserId,
                        "passport",
                        PassportNumber ?? "",
                        PassportFile,
                        Country ?? "",
                        PassportExpiry
                    );

                    documentChanged |= await SaveOrUpdateDocumentAsync(
                        user.UserId,
                        "international_driving_permit",
                        InternationalPermitNumber ?? "",
                        InternationalPermitFile,
                        Country ?? "",
                        InternationalPermitExpiry
                    );
                }

                /*
                    IMPORTANT FEATURE:
                    Notify admin only when documents are newly submitted or updated.
                */
                if (documentChanged)
                {
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
                }

                await _context.SaveChangesAsync();

                HttpContext.Session.SetString("UserName", $"{user.FirstName} {user.LastName}");
                HttpContext.Session.SetString("UserEmail", user.Email);

                TempData["Success"] = documentChanged
                    ? "Profile and verification documents saved successfully. Please wait for admin review."
                    : "Profile saved successfully.";

                return RedirectToPage("/User/Profile/Index", GetRouteValues());
            }
            catch
            {
                TempData["Error"] = "Something went wrong while saving your profile and documents.";
                return Page();
            }
        }

        private object GetRouteValues()
        {
            return new
            {
                carId = CarId,
                borrowDate = BorrowDate,
                borrowTime = BorrowTime,
                returnDate = ReturnDate,
                returnTime = ReturnTime
            };
        }

        private void LoadUserToPage(VentureCarRentals.Models.User user)
        {
            FirstName = user.FirstName;
            MiddleName = user.MiddleName;
            LastName = user.LastName;
            Email = user.Email;
            PhoneNumber = user.PhoneNumber;
            Street = user.Street;
            Barangay = user.Barangay;
            City = user.City;
            State = user.State;
            ZipCode = user.ZipCode;
            Country = string.IsNullOrWhiteSpace(user.Country) ? "Philippines" : user.Country;
            Birthday = user.Birthday;
            CreatedAt = user.CreatedAt;

            /*
                IMPORTANT:
                If saved country is not Philippines, show Foreign mode automatically.
            */
            if (!string.IsNullOrWhiteSpace(user.Country) &&
                !user.Country.Equals("Philippines", StringComparison.OrdinalIgnoreCase))
            {
                RenterType = "foreign";
            }
            else
            {
                RenterType = "local";
            }
        }

        private async Task LoadExistingDocumentsAsync(int userId, bool fillInputs)
        {
            var documents = await _context.UserDocuments
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            var driverLicense = documents.FirstOrDefault(d => d.DocType == "driver_license");

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

            var secondaryId = documents.FirstOrDefault(d => secondaryTypes.Contains(d.DocType));
            var passport = documents.FirstOrDefault(d => d.DocType == "passport");
            var internationalPermit = documents.FirstOrDefault(d => d.DocType == "international_driving_permit");

            if (driverLicense != null)
            {
                ExistingDriverLicenseFileUrl = driverLicense.FileUrl;
                ExistingDriverLicenseStatus = driverLicense.Status;

                if (fillInputs)
                {
                    DriverLicenseNumber = driverLicense.DocNumber;
                    DriverLicenseExpiry = driverLicense.ExpiryDate;
                }
            }

            if (secondaryId != null)
            {
                ExistingSecondaryFileUrl = secondaryId.FileUrl;
                ExistingSecondaryStatus = secondaryId.Status;

                if (fillInputs)
                {
                    SecondaryDocType = secondaryId.DocType;
                    SecondaryDocNumber = secondaryId.DocNumber;
                }
            }

            if (passport != null)
            {
                ExistingPassportFileUrl = passport.FileUrl;
                ExistingPassportStatus = passport.Status;

                if (fillInputs)
                {
                    PassportNumber = passport.DocNumber;
                    PassportExpiry = passport.ExpiryDate;
                }
            }

            if (internationalPermit != null)
            {
                ExistingInternationalPermitFileUrl = internationalPermit.FileUrl;
                ExistingInternationalPermitStatus = internationalPermit.Status;

                if (fillInputs)
                {
                    InternationalPermitNumber = internationalPermit.DocNumber;
                    InternationalPermitExpiry = internationalPermit.ExpiryDate;
                }
            }

            if (fillInputs && (passport != null || internationalPermit != null))
            {
                RenterType = "foreign";
            }
        }

        private void NormalizeInputs()
        {
            FirstName = FirstName?.Trim() ?? "";
            MiddleName = MiddleName?.Trim() ?? "";
            LastName = LastName?.Trim() ?? "";
            Email = Email?.Trim() ?? "";
            PhoneNumber = PhoneNumber?.Trim() ?? "";
            Street = Street?.Trim() ?? "";
            Barangay = Barangay?.Trim() ?? "";
            City = City?.Trim() ?? "";
            State = State?.Trim() ?? "";
            ZipCode = ZipCode?.Trim() ?? "";
            Country = Country?.Trim() ?? "";

            RenterType = string.IsNullOrWhiteSpace(RenterType)
                ? "local"
                : RenterType.ToLower().Trim();

            DriverLicenseNumber = DriverLicenseNumber?.Trim() ?? "";
            SecondaryDocType = SecondaryDocType?.Trim() ?? "";
            SecondaryDocNumber = SecondaryDocNumber?.Trim() ?? "";
            PassportNumber = PassportNumber?.Trim() ?? "";
            InternationalPermitNumber = InternationalPermitNumber?.Trim() ?? "";

            if (RenterType != "local" && RenterType != "foreign")
            {
                RenterType = "local";
            }
        }

        private void ValidateProfileFields()
        {
            /*
                IMPORTANT:
                Profile validation is based on RenterType.

                Foreign renter:
                - Country, City, Zip Code only for address.

                Local renter:
                - Full local address is required.
            */

            if (string.IsNullOrWhiteSpace(FirstName))
            {
                ModelState.AddModelError(nameof(FirstName), "First name is required.");
            }

            if (string.IsNullOrWhiteSpace(LastName))
            {
                ModelState.AddModelError(nameof(LastName), "Last name is required.");
            }

            if (string.IsNullOrWhiteSpace(Email))
            {
                ModelState.AddModelError(nameof(Email), "Email address is required.");
            }

            if (!string.IsNullOrWhiteSpace(Email) && !Email.Contains("@"))
            {
                ModelState.AddModelError(nameof(Email), "Please enter a valid email address.");
            }

            if (string.IsNullOrWhiteSpace(PhoneNumber))
            {
                ModelState.AddModelError(nameof(PhoneNumber), "Phone number is required.");
            }

            if (Birthday == null)
            {
                ModelState.AddModelError(nameof(Birthday), "Birthday is required.");
            }
            else
            {
                if (Birthday.Value.Date > DateTime.Today)
                {
                    ModelState.AddModelError(nameof(Birthday), "Birthday cannot be in the future.");
                }

                /*
                    IMPORTANT FEATURE:
                    Renter must be at least 18 years old.
                */
                if (!AgeValidationHelper.IsAtLeast18(Birthday))
                {
                    ModelState.AddModelError(nameof(Birthday), AgeValidationHelper.UnderAgeMessage);
                }
            }

            if (RenterType == "foreign")
            {
                if (string.IsNullOrWhiteSpace(Country))
                {
                    ModelState.AddModelError(nameof(Country), "Country is required for foreign renters.");
                }

                if (string.IsNullOrWhiteSpace(City))
                {
                    ModelState.AddModelError(nameof(City), "City is required for foreign renters.");
                }

                if (string.IsNullOrWhiteSpace(ZipCode))
                {
                    ModelState.AddModelError(nameof(ZipCode), "Zip code is required for foreign renters.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Street))
                {
                    ModelState.AddModelError(nameof(Street), "Street is required for local renters.");
                }

                if (string.IsNullOrWhiteSpace(Barangay))
                {
                    ModelState.AddModelError(nameof(Barangay), "Barangay is required for local renters.");
                }

                if (string.IsNullOrWhiteSpace(City))
                {
                    ModelState.AddModelError(nameof(City), "City is required.");
                }

                if (string.IsNullOrWhiteSpace(State))
                {
                    ModelState.AddModelError(nameof(State), "State or province is required for local renters.");
                }

                if (string.IsNullOrWhiteSpace(ZipCode))
                {
                    ModelState.AddModelError(nameof(ZipCode), "Zip code is required.");
                }

                if (string.IsNullOrWhiteSpace(Country))
                {
                    ModelState.AddModelError(nameof(Country), "Country is required.");
                }
            }
        }

        private void ValidateDocumentFields()
        {
            /*
                IMPORTANT:
                Document validation is based on RenterType.

                Foreign renter:
                - Passport
                - International Driving Permit

                Local renter:
                - Driver's License
                - One Secondary ID
            */

            if (RenterType == "local")
            {
                if (string.IsNullOrWhiteSpace(DriverLicenseNumber))
                {
                    ModelState.AddModelError(nameof(DriverLicenseNumber), "Driver's license number is required.");
                }

                if (DriverLicenseExpiry == null)
                {
                    ModelState.AddModelError(nameof(DriverLicenseExpiry), "Driver's license expiration date is required.");
                }
                else if (DriverLicenseExpiry.Value.Date <= DateTime.Today)
                {
                    ModelState.AddModelError(nameof(DriverLicenseExpiry), "Driver's license must not be expired.");
                }

                if (!HasDriverLicense && DriverLicenseFile == null)
                {
                    ModelState.AddModelError(nameof(DriverLicenseFile), "Driver's license file is required.");
                }

                ValidateFileIfUploaded(DriverLicenseFile, nameof(DriverLicenseFile));

                if (string.IsNullOrWhiteSpace(SecondaryDocType))
                {
                    ModelState.AddModelError(nameof(SecondaryDocType), "Secondary ID type is required.");
                }

                if (string.IsNullOrWhiteSpace(SecondaryDocNumber))
                {
                    ModelState.AddModelError(nameof(SecondaryDocNumber), "Secondary ID number is required.");
                }

                if (!HasSecondaryId && SecondaryDocFile == null)
                {
                    ModelState.AddModelError(nameof(SecondaryDocFile), "Secondary ID file is required.");
                }

                ValidateFileIfUploaded(SecondaryDocFile, nameof(SecondaryDocFile));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(PassportNumber))
                {
                    ModelState.AddModelError(nameof(PassportNumber), "Passport number is required.");
                }

                if (PassportExpiry == null)
                {
                    ModelState.AddModelError(nameof(PassportExpiry), "Passport expiration date is required.");
                }
                else if (PassportExpiry.Value.Date <= DateTime.Today)
                {
                    ModelState.AddModelError(nameof(PassportExpiry), "Passport must not be expired.");
                }

                if (!HasPassport && PassportFile == null)
                {
                    ModelState.AddModelError(nameof(PassportFile), "Passport file is required.");
                }

                ValidateFileIfUploaded(PassportFile, nameof(PassportFile));

                if (string.IsNullOrWhiteSpace(InternationalPermitNumber))
                {
                    ModelState.AddModelError(nameof(InternationalPermitNumber), "International driving permit number is required.");
                }

                if (InternationalPermitExpiry == null)
                {
                    ModelState.AddModelError(nameof(InternationalPermitExpiry), "International permit expiration date is required.");
                }
                else if (InternationalPermitExpiry.Value.Date <= DateTime.Today)
                {
                    ModelState.AddModelError(nameof(InternationalPermitExpiry), "International permit must not be expired.");
                }

                if (!HasInternationalPermit && InternationalPermitFile == null)
                {
                    ModelState.AddModelError(nameof(InternationalPermitFile), "International permit file is required.");
                }

                ValidateFileIfUploaded(InternationalPermitFile, nameof(InternationalPermitFile));
            }
        }

        private void UpdateUserProfile(VentureCarRentals.Models.User user)
        {
            user.FirstName = FirstName ?? "";
            user.MiddleName = MiddleName ?? "";
            user.LastName = LastName ?? "";
            user.Email = Email ?? "";
            user.PhoneNumber = PhoneNumber ?? "";
            user.City = City ?? "";
            user.ZipCode = ZipCode ?? "";
            user.Country = Country ?? "";
            user.Birthday = Birthday;

            /*
                IMPORTANT:
                Foreign renters only need Country, City, and Zip Code.
                Local-only address fields are cleared for foreign renters.
            */
            if (RenterType == "foreign")
            {
                user.Street = "";
                user.Barangay = "";
                user.State = "";
            }
            else
            {
                user.Street = Street ?? "";
                user.Barangay = Barangay ?? "";
                user.State = State ?? "";
            }
        }

        private void ValidateFileIfUploaded(IFormFile? file, string fieldName)
        {
            if (file == null)
            {
                return;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(fieldName, "Only JPG, PNG, and PDF files are allowed.");
            }

            var maxSize = 50 * 1024 * 1024;

            if (file.Length > maxSize)
            {
                ModelState.AddModelError(fieldName, "Maximum file size is 50 MB.");
            }
        }

        private async Task<bool> SaveOrUpdateDocumentAsync(
            int userId,
            string docType,
            string docNumber,
            IFormFile? file,
            string issuingCountry,
            DateTime? expiryDate)
        {
            var document = await _context.UserDocuments
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DocType == docType);

            var changed = false;

            if (document == null)
            {
                document = new UserDocument
                {
                    UserId = userId,
                    DocType = docType,
                    UploadedAt = DateTime.Now
                };

                _context.UserDocuments.Add(document);
                changed = true;
            }

            if (document.DocNumber != docNumber)
            {
                document.DocNumber = docNumber;
                changed = true;
            }

            if (document.IssuingCountry != issuingCountry)
            {
                document.IssuingCountry = issuingCountry;
                changed = true;
            }

            if (document.ExpiryDate != expiryDate)
            {
                document.ExpiryDate = expiryDate;
                changed = true;
            }

            if (file != null)
            {
                document.FileUrl = await SaveUploadedFileAsync(userId, file);
                changed = true;
            }

            /*
                IMPORTANT:
                Any updated or newly uploaded document becomes pending again.
                Admin must review updated documents again.
            */
            if (changed)
            {
                document.Status = "pending";
                document.UploadedAt = DateTime.Now;
            }

            return changed;
        }

        private async Task<string> SaveUploadedFileAsync(int userId, IFormFile file)
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

            return $"/uploads/documents/{fileName}";
        }
    }
}