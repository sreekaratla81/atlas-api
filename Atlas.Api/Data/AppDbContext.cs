using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace Atlas.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // Use cascade deletes for integration tests. In other environments
            // apply cascade to only one FK to avoid multiple cascade paths.

            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountReceived)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.ExtraGuestCharge)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountGuestPaid)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.CommissionAmount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Property>()
                .Property(p => p.CommissionPercent)
                .HasPrecision(5, 2);


            if (env == "IntegrationTest")
            {
                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.Guest)
                    .WithMany()
                    .HasForeignKey(b => b.GuestId)
                    .OnDelete(DeleteBehavior.Cascade);

                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.Listing)
                    .WithMany(l => l.Bookings)
                    .HasForeignKey(b => b.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);

                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.BankAccount)
                    .WithMany()
                    .HasForeignKey(b => b.BankAccountId)
                    .OnDelete(DeleteBehavior.Cascade);

            }
            else
            {
                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.Guest)
                    .WithMany()
                    .HasForeignKey(b => b.GuestId)
                    .OnDelete(DeleteBehavior.Restrict);

                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.Listing)
                    .WithMany(l => l.Bookings)
                    .HasForeignKey(b => b.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);

                modelBuilder.Entity<Booking>()
                    .HasOne(b => b.BankAccount)
                    .WithMany()
                    .HasForeignKey(b => b.BankAccountId)
                    .OnDelete(DeleteBehavior.Restrict);

            }
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
    }
}
