using Microsoft.EntityFrameworkCore;
using VentureCarRentals.Models;

namespace VentureCarRentals.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<RentalAgreement> RentalAgreements { get; set; }
        public DbSet<UserDocument> UserDocuments { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; }
        public DbSet<UserPaymentMethod> UserPaymentMethods { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.BookingId)
                .IsUnique();

            modelBuilder.Entity<RentalAgreement>()
                .HasIndex(r => r.BookingId)
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.BookingId)
                .IsUnique();
        }
    }
}