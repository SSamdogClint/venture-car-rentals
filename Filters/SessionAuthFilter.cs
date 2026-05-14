using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VentureCarRentals.Filters
{
    public class SessionAuthFilter : IAsyncPageFilter
    {
        private readonly string _requiredRole;
        private readonly string _loginPage;

        public SessionAuthFilter(string requiredRole, string loginPage = "/Login")
        {
            _requiredRole = requiredRole;
            _loginPage = loginPage;
        }

        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            return Task.CompletedTask;
        }

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            /*
                // IMPORTANT SS THIS:
                Get the current page path.

                Example:
                /Login
                /Admin/Dashboard
                /User/Cars/BrowseCars
                /Guest/Cars/BrowseCars

                ToLower() makes the checking case-insensitive.
                TrimEnd('/') prevents issues if the URL ends with slash.
            */
            var path = context.HttpContext.Request.Path.Value?.ToLower().TrimEnd('/') ?? "";

            /*
                // IMPORTANT SS THIS:
                These pages are public.

                Public pages can be opened even if the visitor is not logged in.

                Guest Browse Cars is public:
                    /Guest/Cars/BrowseCars

                Private User Browse Cars is NOT public:
                    /User/Cars/BrowseCars
            */
            var publicPages = new[]
            {
                "",
                "/",
                "/index",
                "/login",
                "/register",
                "/logout",

                /*
                    // IMPORTANT SS THIS:
                    This is the separated guest/public car catalog page.

                    Guests can view cars here without logging in.
                    If guest clicks Book or Join Us, they should go to /Index.
                */
                "/guest/cars/browsecars"
            };

            /*
                // IMPORTANT SS THIS:
                If the current page is public, skip login checking.
            */
            if (publicPages.Contains(path))
            {
                await next();
                return;
            }

            /*
                // IMPORTANT SS THIS:
                Disable browser caching for protected pages.

                This helps prevent the browser Back button from showing
                old Admin/User pages after logout.
            */
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.HttpContext.Response.Headers["Pragma"] = "no-cache";
            context.HttpContext.Response.Headers["Expires"] = "0";

            /*
                // IMPORTANT SS THIS:
                These session values must match what you set during login.

                Example after successful login:
                    Session["UserId"] = user.UserId
                    Session["UserRole"] = "Admin" or "User"
            */
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var userRole = context.HttpContext.Session.GetString("UserRole");

            /*
                // IMPORTANT SS THIS:
                This checks if the visitor is logged in and has the correct role.

                For Admin protected pages:
                    required role = Admin

                For User protected pages:
                    required role = User
            */
            var isAllowed =
                userId != null &&
                string.Equals(userRole, _requiredRole, StringComparison.OrdinalIgnoreCase);

            /*
                // IMPORTANT SS THIS:
                If there is no session or the role is wrong,
                redirect the visitor to the login page.

                Example:
                Guest tries to open /User/Cars/BrowseCars
                    -> redirect to /Login

                User tries to open /Admin/Dashboard
                    -> redirect to /Login
            */
            if (!isAllowed)
            {
                context.Result = new RedirectToPageResult(_loginPage);
                return;
            }

            await next();
        }
    }
}