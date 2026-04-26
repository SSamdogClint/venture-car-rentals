using VentureCarRentals.Models;

namespace VentureCarRentals.Data
{
    public static class DbSeeder
    {
        public static void SeedAdmin(AppDbContext context)
        {
            string adminEmail = "admin@venture.com";

            var adminExists = context.Users.Any(u => u.Email == adminEmail);

            if (!adminExists)
            {
                var admin = new User
                {
                    FirstName = "System",
                    LastName = "Admin",
                    Email = adminEmail,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    IsAdmin = true
                };

                context.Users.Add(admin);
                context.SaveChanges();
            }
        }
    }
}