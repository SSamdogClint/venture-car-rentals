using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using VentureCarRentals.Data;
using VentureCarRentals.Filters;

var builder = WebApplication.CreateBuilder(args);

// QuestPDF license setting for generated rental agreement PDFs.
QuestPDF.Settings.License = LicenseType.Community;

builder.Services.AddRazorPages(options =>
{
    /*
        IMPORTANT:
        This protects all pages inside /Pages/Admin.

        Example protected pages:
        /Admin/Dashboard
        /Admin/Bookings/BookingList
        /Admin/Maintenance/Index
    */
    options.Conventions.AddFolderApplicationModelConvention("/Admin", model =>
    {
        model.Filters.Add(new SessionAuthFilter("Admin", "/Login"));
    });

    /*
        IMPORTANT:
        This protects all pages inside /Pages/User.

        Example protected pages:
        /User/Home
        /User/Bookings/Index
        /User/Payments/Index
    */
    options.Conventions.AddFolderApplicationModelConvention("/User", model =>
    {
        model.Filters.Add(new SessionAuthFilter("User", "/Login"));
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSession();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

/*
    IMPORTANT FEATURE:
    This protects all /Admin pages.

    Guest users are redirected to Login.
    Logged-in non-admin users are redirected to User Home.
*/
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower();

    if (!string.IsNullOrEmpty(path) && path.StartsWith("/admin"))
    {
        var userId = context.Session.GetInt32("UserId");
        var isAdmin = context.Session.GetString("IsAdmin");

        if (userId == null)
        {
            Console.WriteLine($"[ACCESS DENIED] Guest attempted to access admin page: {context.Request.Path}");

            context.Response.Redirect("/Login");
            return;
        }

        if (!string.Equals(isAdmin, "true", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ACCESS DENIED] UserId {userId} attempted to access admin page: {context.Request.Path}");

            context.Response.Redirect("/User/Home");
            return;
        }
    }

    await next();
});

app.UseAuthorization();

/*
    IMPORTANT FEATURE:
    The root URL "/" will open BrowseCars first.

    Example:
    https://localhost:7173/
    redirects to:
    https://localhost:7173/User/Cars/BrowseCars

    Your /Index page will still work for Sign Up / Sign In.
*/
app.MapGet("/", () => Results.Redirect("/Guest/Cars/BrowseCars"));

app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.SeedAdmin(context);
}

app.Run();