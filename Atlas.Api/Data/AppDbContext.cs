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

            // All environments use DeleteBehavior.Restrict to avoid accidental
            // cascading deletes. Integration tests explicitly clean up related
            // entities when necessary.

            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountReceived)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.ExtraGuestCharge)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.CommissionAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.TotalAmount)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.BookingStatus)
                .HasDefaultValue("Lead");
            modelBuilder.Entity<Booking>()
                .Property(b => b.Currency)
                .HasDefaultValue("INR");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Property>()
                .Property(p => p.CommissionPercent)
                .HasPrecision(5, 2);

            modelBuilder.Entity<ListingBasePrice>()
                .Property(bp => bp.BasePrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingBasePrice>()
                .Property(bp => bp.Currency)
                .HasDefaultValue("INR");

            modelBuilder.Entity<ListingBasePrice>()
                .HasIndex(bp => bp.ListingId)
                .IsUnique();

            modelBuilder.Entity<ListingDailyOverride>()
                .Property(o => o.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingDailyOverride>()
                .HasIndex(o => new { o.ListingId, o.Date })
                .IsUnique();

            modelBuilder.Entity<ListingBasePrice>()
                .HasOne(bp => bp.Listing)
                .WithOne(l => l.BasePrice)
                .HasForeignKey<ListingBasePrice>(bp => bp.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ListingDailyOverride>()
                .HasOne(o => o.Listing)
                .WithMany(l => l.DailyOverrides)
                .HasForeignKey(o => o.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.Status)
                .HasDefaultValue("Active");

            modelBuilder.Entity<AvailabilityBlock>()
                .HasIndex(ab => new { ab.ListingId, ab.StartDate, ab.EndDate });

            modelBuilder.Entity<AvailabilityBlock>()
                .HasIndex(ab => ab.BookingId);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Guest)
                .WithMany()
                .HasForeignKey(b => b.GuestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Listing)
                .WithMany(l => l.Bookings)
                .HasForeignKey(b => b.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.BankAccount)
                .WithMany()
                .HasForeignKey(b => b.BankAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AvailabilityBlock>()
                .HasOne(ab => ab.Listing)
                .WithMany()
                .HasForeignKey(ab => ab.ListingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AvailabilityBlock>()
                .HasOne(ab => ab.Booking)
                .WithMany()
                .HasForeignKey(ab => ab.BookingId)
                .OnDelete(DeleteBehavior.Restrict);
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<AvailabilityBlock> AvailabilityBlocks { get; set; }
        public DbSet<ListingBasePrice> ListingBasePrices { get; set; }
        public DbSet<ListingDailyOverride> ListingDailyOverrides { get; set; }
    }
}
