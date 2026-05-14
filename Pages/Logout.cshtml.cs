using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentureCarRentals.Pages
{
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
        {
            /*
                IMPORTANT:
                This clears all session values:
                UserId, UserRole, UserName, etc.
            */
            HttpContext.Session.Clear();

            /*
                IMPORTANT:
                This prevents browser cache from keeping protected pages
                after logout.
            */
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            return RedirectToPage("/Guest/Cars/BrowseCars");
        }

        public IActionResult OnPost()
        {
            return OnGet();
        }
    }
}