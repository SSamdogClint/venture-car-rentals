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
                IMPORTANT:
                This disables browser caching for protected pages.

                This helps prevent the browser Back button from showing
                old Admin/User pages after logout.
            */
            context.HttpContext.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.HttpContext.Response.Headers["Pragma"] = "no-cache";
            context.HttpContext.Response.Headers["Expires"] = "0";

            /*
                IMPORTANT:
                These session values must match what you set during login.

                Example after login:
                Session["UserId"] = user.UserId
                Session["UserRole"] = "Admin" or "User"
            */
            var userId = context.HttpContext.Session.GetInt32("UserId");
            var userRole = context.HttpContext.Session.GetString("UserRole");

            /*
                IMPORTANT:
                This checks if someone is logged in and has the correct role.

                For /Admin pages:
                required role = Admin

                For /User pages:
                required role = User
            */
            var isAllowed =
                userId != null &&
                string.Equals(userRole, _requiredRole, StringComparison.OrdinalIgnoreCase);

            /*
                IMPORTANT:
                If the session is gone or the role is wrong,
                redirect the visitor back to the login page.
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