using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Data;

var builder = WebApplication.CreateBuilder(args);

// Registers Razor Pages for the web application.
builder.Services.AddRazorPages();

// Registers the database context and configures SQLite as the database provider.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Enables session storage.
// The system uses session to store logged-in user information such as UserId and IsAdmin.
builder.Services.AddSession();

var app = builder.Build();

// Uses a general error page when the application is not running in development mode.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Allows the application to serve static files such as CSS, JavaScript, images, and uploaded files.
app.UseStaticFiles();

// Enables request routing.
app.UseRouting();

// Enables session before checking protected routes.
// This must be placed before the custom admin access middleware.
app.UseSession();

/*
    Admin Route Protection Middleware

    Purpose:
    This middleware prevents normal users from manually accessing admin pages
    by typing URLs such as /Admin/Dashboard or /Admin/Users/UserControl.

    Security Logic:
    1. If the requested URL starts with /Admin, the system checks the session.
    2. If no UserId exists in session, the user is not logged in and is redirected to Login.
    3. If the user is logged in but IsAdmin is not true, the user is redirected to User Home.
    4. Only users with IsAdmin = true can continue to admin pages.
*/



// Admin Access Control Middleware

app.Use(async (context, next) =>
{
    var requestPath = context.Request.Path.Value?.ToLower();

    if (!string.IsNullOrWhiteSpace(requestPath) && requestPath.StartsWith("/admin"))
    {
        var userId = context.Session.GetInt32("UserId");
        var isAdmin = context.Session.GetString("IsAdmin");

        // Case 1: User is not logged in.
        if (userId == null)
        {
            Console.WriteLine(
                $"[ACCESS DENIED] Unauthenticated user attempted to access a protected admin route. " +
                $"Path: {context.Request.Path} | Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            );

            context.Response.Redirect("/Login");
            return;
        }

        // Case 2: User is logged in but does not have admin privileges.
        if (!string.Equals(isAdmin, "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine(
                $"[ACCESS DENIED] Non-admin user attempted to access a protected admin route. " +
                $"Path: {context.Request.Path} | Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
            );

            context.Response.Redirect("/User/Home");
            return;
        }

        // Case 3: User is authenticated and authorized as admin.
        Console.WriteLine(
            $"[ACCESS GRANTED] Admin user accessed admin route. " +
            $"Path: {context.Request.Path} | Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
        );
    }

    await next();
});

// Enables authorization middleware.
// This is included for ASP.NET Core security pipeline support.
app.UseAuthorization();

// Maps Razor Pages endpoints.
app.MapRazorPages();

/*
    Database Seeding

    Purpose:
    This creates the default admin account if it does not exist yet.
    It helps ensure that the system always has an admin user after setup.
*/
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.SeedAdmin(context);
}

// Starts the web application.
app.Run();