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
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.BookingStatus)
                .HasColumnType("varchar(20)")
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue("Lead");
            modelBuilder.Entity<Booking>()
                .Property(b => b.Currency)
                .HasColumnType("varchar(10)")
                .HasMaxLength(10)
                .IsRequired()
                .HasDefaultValue("INR");
            modelBuilder.Entity<Booking>()
                .Property(b => b.ExternalReservationId)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);
            modelBuilder.Entity<Booking>()
                .Property(b => b.ConfirmationSentAtUtc)
                .HasColumnType("datetime");
            modelBuilder.Entity<Booking>()
                .Property(b => b.RefundFreeUntilUtc)
                .HasColumnType("datetime");
            modelBuilder.Entity<Booking>()
                .Property(b => b.CheckedInAtUtc)
                .HasColumnType("datetime");
            modelBuilder.Entity<Booking>()
                .Property(b => b.CheckedOutAtUtc)
                .HasColumnType("datetime");
            modelBuilder.Entity<Booking>()
                .Property(b => b.CancelledAtUtc)
                .HasColumnType("datetime");
            modelBuilder.Entity<Booking>()
                .Property(b => b.BookingSource)
                .HasColumnType("varchar(50)")
                .HasMaxLength(50);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Property>()
                .Property(p => p.CommissionPercent)
                .HasPrecision(5, 2);

            modelBuilder.Entity<ListingPricing>()
                .ToTable("ListingPricing");

            modelBuilder.Entity<ListingPricing>()
                .HasKey(p => p.ListingId);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.BaseNightlyRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.WeekendNightlyRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.ExtraGuestRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.Currency)
                .HasMaxLength(10)
                .HasColumnType("varchar(10)")
                .HasDefaultValue("INR");

            modelBuilder.Entity<ListingPricing>()
                .Property(p => p.UpdatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ListingDailyRate>()
                .ToTable("ListingDailyRate");

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.NightlyRate)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.Currency)
                .HasMaxLength(10)
                .HasColumnType("varchar(10)")
                .HasDefaultValue("INR");

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.Source)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.Reason)
                .HasMaxLength(200)
                .HasColumnType("varchar(200)");

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.UpdatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ListingDailyRate>()
                .HasIndex(r => new { r.ListingId, r.Date })
                .IsUnique();

            modelBuilder.Entity<ListingDailyRate>()
                .Property(r => r.Date)
                .HasColumnType("date");

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
                .ToTable("AvailabilityBlock");

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.StartDate)
                .HasColumnType("date");

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.EndDate)
                .HasColumnType("date");

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.BlockType)
                .HasMaxLength(30)
                .HasColumnType("varchar(30)")
                .IsRequired();

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.Source)
                .HasMaxLength(30)
                .HasColumnType("varchar(30)")
                .IsRequired();

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.Status)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .HasDefaultValue("Active");

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.CreatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<AvailabilityBlock>()
                .Property(ab => ab.UpdatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<AvailabilityBlock>()
                .HasIndex(ab => new { ab.ListingId, ab.StartDate, ab.EndDate });

            modelBuilder.Entity<AvailabilityBlock>()
                .HasIndex(ab => ab.BookingId);

            modelBuilder.Entity<EnvironmentMarker>()
                .ToTable("EnvironmentMarker");

            modelBuilder.Entity<EnvironmentMarker>()
                .Property(em => em.Marker)
                .HasColumnType("varchar(10)")
                .HasMaxLength(10)
                .IsRequired();

            modelBuilder.Entity<EnvironmentMarker>()
                .HasIndex(em => em.Marker)
                .IsUnique();

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
                .ToTable("CommunicationLog");

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.Channel)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.EventType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.ToAddress)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.CorrelationId)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.IdempotencyKey)
                .HasMaxLength(150)
                .HasColumnType("varchar(150)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.Provider)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.ProviderMessageId)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)");

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.Status)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.LastError)
                .HasColumnType("text");

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.CreatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<CommunicationLog>()
                .Property(cl => cl.SentAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<CommunicationLog>()
                .HasIndex(cl => cl.IdempotencyKey)
                .IsUnique();

            modelBuilder.Entity<CommunicationLog>()
                .HasOne(cl => cl.Booking)
                .WithMany()
                .HasForeignKey(cl => cl.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<CommunicationLog>()
                .HasOne(cl => cl.Guest)
                .WithMany()
                .HasForeignKey(cl => cl.GuestId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<CommunicationLog>()
                .HasOne(cl => cl.MessageTemplate)
                .WithMany()
                .HasForeignKey(cl => cl.TemplateId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<MessageTemplate>()
                .ToTable("MessageTemplate");

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.TemplateKey)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)");

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.EventType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Channel)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.ScopeType)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Language)
                .HasMaxLength(10)
                .HasColumnType("varchar(10)")
                .IsRequired();

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Subject)
                .HasMaxLength(200)
                .HasColumnType("varchar(200)");

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.Body)
                .HasColumnType("text")
                .IsRequired();

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.CreatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<MessageTemplate>()
                .Property(mt => mt.UpdatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<OutboxMessage>()
                .ToTable("OutboxMessage");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.AggregateType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.AggregateId)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.EventType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.PayloadJson)
                .HasColumnType("text")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.HeadersJson)
                .HasColumnType("text");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.CreatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.PublishedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.LastError)
                .HasColumnType("text");

            modelBuilder.Entity<AutomationSchedule>()
                .ToTable("AutomationSchedule");

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.Id)
                .HasColumnType("bigint");

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.EventType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)")
                .IsRequired();

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.DueAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.Status)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.PublishedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.CompletedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<AutomationSchedule>()
                .Property(a => a.LastError)
                .HasColumnType("text");
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
        public DbSet<ListingPricing> ListingPricings { get; set; }
        public DbSet<ListingDailyRate> ListingDailyRates { get; set; }
        public DbSet<MessageTemplate> MessageTemplates { get; set; }
        public DbSet<CommunicationLog> CommunicationLogs { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<AutomationSchedule> AutomationSchedules { get; set; }
        public DbSet<EnvironmentMarker> EnvironmentMarkers { get; set; }
    }
}
