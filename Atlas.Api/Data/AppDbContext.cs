using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using System;
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

            var deleteBehavior = ResolveDeleteBehavior();

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

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.BaseRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.WeekdayRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.WeekendRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.Currency)
                .HasDefaultValue("INR");

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ListingPricing>()
                .HasIndex(p => p.ListingId)
                .IsUnique();

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.Rate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ListingDailyRate>()
                .HasIndex(r => new { r.ListingId, r.Date })
                .IsUnique();

            modelBuilder.Entity<ListingBasePrice>()
                .HasOne(bp => bp.Listing)
                .WithOne(l => l.BasePrice)
                .HasForeignKey<ListingBasePrice>(bp => bp.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<ListingDailyOverride>()
                .HasOne(o => o.Listing)
                .WithMany(l => l.DailyOverrides)
                .HasForeignKey(o => o.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<ListingPricing>()
                .HasOne(p => p.Listing)
                .WithOne(l => l.Pricing)
                .HasForeignKey<ListingPricing>(p => p.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<ListingDailyRate>()
                .HasOne(r => r.Listing)
                .WithMany(l => l.DailyRates)
                .HasForeignKey(r => r.ListingId)
                .OnDelete(deleteBehavior);

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
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Listing)
                .WithMany(l => l.Bookings)
                .HasForeignKey(b => b.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.BankAccount)
                .WithMany()
                .HasForeignKey(b => b.BankAccountId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<AvailabilityBlock>()
                .HasOne(ab => ab.Listing)
                .WithMany()
                .HasForeignKey(ab => ab.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<AvailabilityBlock>()
                .HasOne(ab => ab.Booking)
                .WithMany()
                .HasForeignKey(ab => ab.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<CommunicationLog>()
                .HasOne(cl => cl.Booking)
                .WithMany()
                .HasForeignKey(cl => cl.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<CommunicationLog>()
                .HasOne(cl => cl.MessageTemplate)
                .WithMany()
                .HasForeignKey(cl => cl.MessageTemplateId)
                .OnDelete(deleteBehavior);
        }

        private static DeleteBehavior ResolveDeleteBehavior()
        {
            var value = Environment.GetEnvironmentVariable("ATLAS_DELETE_BEHAVIOR");
            return string.Equals(value, "Cascade", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                ? DeleteBehavior.Cascade
                : DeleteBehavior.Restrict;
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
        public DbSet<ListingPricing> ListingPricings { get; set; }
        public DbSet<ListingDailyRate> ListingDailyRates { get; set; }
        public DbSet<MessageTemplate> MessageTemplates { get; set; }
        public DbSet<CommunicationLog> CommunicationLogs { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<AutomationSchedule> AutomationSchedules { get; set; }
    }
}
