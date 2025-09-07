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

            modelBuilder.Entity<Booking>()
                .Property(b => b.AmountReceived)
                .HasPrecision(18, 2);
            modelBuilder.Entity<Booking>()
                .Property(b => b.ExtraGuestCharge)
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

            modelBuilder.Entity<Listing>(e =>
            {
                e.Property(p => p.Slug).HasMaxLength(128).IsRequired();
                e.HasIndex(p => p.Slug).IsUnique();
                e.Property(p => p.BlobContainer).HasMaxLength(63).IsRequired();
                e.Property(p => p.BlobPrefix).HasMaxLength(256).IsRequired();
                e.Property(p => p.CoverImage).HasMaxLength(256);
                e.Property(p => p.ShortDescription).HasMaxLength(400);
                e.Property(p => p.NightlyPrice).HasColumnType("decimal(10,2)");
            });

            modelBuilder.Entity<ListingMedia>(e =>
            {
                e.Property(p => p.BlobName).HasMaxLength(256).IsRequired();
                e.Property(p => p.Caption).HasMaxLength(200);
                e.HasIndex(p => new { p.ListingId, p.BlobName }).IsUnique();
                e.HasIndex(p => p.ListingId);
                e.HasIndex(p => new { p.ListingId, p.IsCover })
                    .IsUnique()
                    .HasFilter("[IsCover] = 1");
                e.HasOne(p => p.Listing)
                    .WithMany(l => l.Media)
                    .HasForeignKey(p => p.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Booking>()
                .HasIndex(x => new { x.ListingId, x.CheckinDate, x.CheckoutDate });

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Guest)
                .WithMany(g => g.Bookings)
                .HasForeignKey(b => b.GuestId)
                .OnDelete(DeleteBehavior.Cascade);

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
        }

        public DbSet<Property> Properties { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<Guest> Guests { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Incident> Incidents { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<ListingMedia> ListingMedia { get; set; }
    }
}
