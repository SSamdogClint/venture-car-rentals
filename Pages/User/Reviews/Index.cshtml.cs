using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.User.Reviews
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<ReviewListViewModel> Reviews { get; set; } = new();

        public int TotalReviews { get; set; }

        public int FiveStarReviews { get; set; }

        public int ReviewedCars { get; set; }

        public double AverageRating { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            /*
                IMPORTANT:
                This query uses joins instead of navigation properties.

                This is safer because it works even if your Review model
                does not have Review.User, Review.Car, or Review.Booking navigation properties.
            */
            Reviews = await (
                from review in _context.Reviews
                join user in _context.Users
                    on review.UserId equals user.UserId
                join car in _context.Cars
                    on review.CarId equals car.CarId
                join booking in _context.Bookings
                    on review.BookingId equals booking.BookingId
                orderby review.CreatedAt descending
                select new ReviewListViewModel
                {
                    ReviewId = review.ReviewId,
                    BookingId = review.BookingId,
                    CarId = review.CarId,

                    CarName = car.Make + " " + car.Model,
                    Category = car.Category,

                    ReviewerName = user.FirstName + " " + user.LastName,
                    Initials =
                        (string.IsNullOrWhiteSpace(user.FirstName) ? "" : user.FirstName.Substring(0, 1)) +
                        (string.IsNullOrWhiteSpace(user.LastName) ? "" : user.LastName.Substring(0, 1)),

                    Rating = review.Rating,
                    Comment = review.Comment ?? "",
                    CreatedAt = review.CreatedAt
                }
            ).ToListAsync();

            TotalReviews = Reviews.Count;

            FiveStarReviews = Reviews.Count(r => r.Rating == 5);

            ReviewedCars = Reviews
                .Select(r => r.CarId)
                .Distinct()
                .Count();

            AverageRating = Reviews.Any()
                ? Reviews.Average(r => r.Rating)
                : 0;

            return Page();
        }
    }

    public class ReviewListViewModel
    {
        public int ReviewId { get; set; }

        public int BookingId { get; set; }

        public int CarId { get; set; }

        public string CarName { get; set; } = "";

        public string Category { get; set; } = "";

        public string ReviewerName { get; set; } = "";

        public string Initials { get; set; } = "";

        public int Rating { get; set; }

        public string Comment { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}