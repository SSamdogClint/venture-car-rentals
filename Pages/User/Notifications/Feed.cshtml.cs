using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

namespace VentureCarRentals.Pages.User.Notifications
{
    public class FeedModel : PageModel
    {
        private readonly AppDbContext _context;

        public FeedModel(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> OnGetLatestAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return new JsonResult(new
                {
                    unreadCount = 0,
                    notifications = new List<object>()
                });
            }

            var unreadCount = await _context.Notifications
                .CountAsync(n =>
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value &&
                    !n.IsRead);

            var notifications = await _context.Notifications
                .Where(n =>
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value)
                .OrderByDescending(n => n.CreatedAt)
                .Take(6)
                .Select(n => new
                {
                    id = n.NotificationId,
                    title = n.Title,
                    message = n.Message,
                    type = n.Type,
                    targetUrl = n.TargetUrl,
                    isRead = n.IsRead,
                    createdAt = n.CreatedAt.ToString("MMM dd, yyyy hh:mm tt")
                })
                .ToListAsync();

            return new JsonResult(new
            {
                unreadCount,
                notifications
            });
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    targetUrl = "#"
                });
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value);

            if (notification == null)
            {
                return new JsonResult(new
                {
                    success = false,
                    targetUrl = "#"
                });
            }

            notification.IsRead = true;

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true,
                targetUrl = notification.TargetUrl
            });
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return new JsonResult(new
                {
                    success = false
                });
            }

            var unreadNotifications = await _context.Notifications
                .Where(n =>
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value &&
                    !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            return new JsonResult(new
            {
                success = true
            });
        }
    }
}