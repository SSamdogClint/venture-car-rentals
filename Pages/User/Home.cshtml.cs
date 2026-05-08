using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.User
{
    public class HomeModel : PageModel
    {
        private readonly AppDbContext _context;

        public HomeModel(AppDbContext context)
        {
            _context = context;
        }

        public bool IsLoggedIn { get; set; }

        public List<HomeReviewViewModel> LatestReviews { get; set; } = new();

        public async Task OnGetAsync()
        {
            // Checks if the visitor is already logged in using session.
            IsLoggedIn = HttpContext.Session.GetInt32("UserId") != null;

            // Load only the latest 4 reviews to display on the home page.
            LatestReviews = await _context.Reviews
                .Include(r => r.User)
                .Include(r => r.Car)
                .OrderByDescending(r => r.CreatedAt)
                .Take(4)
                .Select(r => new HomeReviewViewModel
                {
                    ReviewId = r.ReviewId,
                    Rating = r.Rating,
                    Comment = string.IsNullOrWhiteSpace(r.Comment)
                        ? "No comment provided."
                        : r.Comment,

                    CreatedAt = r.CreatedAt,

                    ReviewerName = r.User == null
                        ? "Customer"
                        : $"{r.User.FirstName} {r.User.LastName}",

                    CarName = r.Car == null
                        ? "Car Rental"
                        : $"{r.Car.Make} {r.Car.Model}",

                    Initials = r.User == null
                        ? "C"
                        : GetInitials(r.User.FirstName, r.User.LastName)
                })
                .ToListAsync();
        }

        private static string GetInitials(string firstName, string lastName)
        {
            // Creates simple initials like "JD" for Juan Dela Cruz.
            var firstInitial = string.IsNullOrWhiteSpace(firstName)
                ? ""
                : firstName.Trim()[0].ToString().ToUpper();

            var lastInitial = string.IsNullOrWhiteSpace(lastName)
                ? ""
                : lastName.Trim()[0].ToString().ToUpper();

            var initials = firstInitial + lastInitial;

            return string.IsNullOrWhiteSpace(initials)
                ? "C"
                : initials;
        }
    }

    public class HomeReviewViewModel
    {
        public int ReviewId { get; set; }

        public int Rating { get; set; }

        public string Comment { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public string ReviewerName { get; set; } = "";

        public string CarName { get; set; } = "";

        public string Initials { get; set; } = "";
    }
}