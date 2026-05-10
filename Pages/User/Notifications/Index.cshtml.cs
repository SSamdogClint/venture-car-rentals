using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.User.Notifications
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _context;

        public IndexModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Notification> Notifications { get; set; } = new();

        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all";

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            await LoadNotificationsAsync(userId.Value);

            return Page();
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value);

            if (notification != null)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
                TempData["Success"] = "Notification marked as read.";
            }

            return RedirectToPage(new { filter = Filter });
        }

        public async Task<IActionResult> OnPostMarkAllReadAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
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

            TempData["Success"] = "All notifications marked as read.";
            return RedirectToPage(new { filter = Filter });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");

            if (userId == null)
            {
                return RedirectToPage("/Login");
            }

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.RecipientType == "user" &&
                    n.UserId == userId.Value);

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Notification deleted.";
            }

            return RedirectToPage(new { filter = Filter });
        }

        private async Task LoadNotificationsAsync(int userId)
        {
            Filter = NormalizeFilter(Filter);

            var query = _context.Notifications
                .Where(n =>
                    n.RecipientType == "user" &&
                    n.UserId == userId)
                .AsQueryable();

            AllCount = await query.CountAsync();
            UnreadCount = await query.CountAsync(n => !n.IsRead);
            ReadCount = await query.CountAsync(n => n.IsRead);

            if (Filter == "unread")
            {
                query = query.Where(n => !n.IsRead);
            }
            else if (Filter == "read")
            {
                query = query.Where(n => n.IsRead);
            }

            Notifications = await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        private static string NormalizeFilter(string? filter)
        {
            return filter?.ToLower().Trim() switch
            {
                "unread" => "unread",
                "read" => "read",
                _ => "all"
            };
        }
    }
}