using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;
using VentureCarRentals.Models;

namespace VentureCarRentals.Pages.Admin
{
    public class NotificationsModel : PageModel
    {
        private readonly AppDbContext _context;

        public NotificationsModel(AppDbContext context)
        {
            _context = context;
        }

        public List<Notification> Notifications { get; set; } = new();

        public int AllCount { get; set; }
        public int UnreadCount { get; set; }
        public int ReadCount { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Filter { get; set; } = "all";

        public async Task OnGetAsync()
        {
            await LoadNotificationsAsync();
        }

        public async Task<IActionResult> OnPostMarkReadAsync(int id)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.RecipientType == "admin");

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
            var unreadNotifications = await _context.Notifications
                .Where(n => n.RecipientType == "admin" && !n.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "All admin notifications marked as read.";
            return RedirectToPage(new { filter = Filter });
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id)
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.NotificationId == id &&
                    n.RecipientType == "admin");

            if (notification != null)
            {
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Notification deleted.";
            }

            return RedirectToPage(new { filter = Filter });
        }

        private async Task LoadNotificationsAsync()
        {
            Filter = NormalizeFilter(Filter);

            var query = _context.Notifications
                .Where(n => n.RecipientType == "admin")
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