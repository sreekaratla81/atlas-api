using Atlas.Api.Models;
using Atlas.Api.Models.Billing;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Atlas.Api.Data
{
    public class AppDbContext : DbContext
    {
        /// <summary>Sentinel value returned when no tenant context is available (background services). Use IgnoreQueryFilters() instead.</summary>
        private const int FallbackTenantId = 0;
        private readonly ITenantContextAccessor? _tenantContextAccessor;

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : this(options, null)
        {
        }

        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContextAccessor? tenantContextAccessor)
            : base(options)
        {
            _tenantContextAccessor = tenantContextAccessor;
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Strategy: global tenant query filters centralize tenant isolation so all EF queries stay in-tenant by default.
            ApplyTenantQueryFilter<Property>(modelBuilder);
            ApplyTenantQueryFilter<Listing>(modelBuilder);
            ApplyTenantQueryFilter<Booking>(modelBuilder);
            ApplyTenantQueryFilter<Guest>(modelBuilder);
            ApplyTenantQueryFilter<Payment>(modelBuilder);
            ApplyTenantQueryFilter<User>(modelBuilder);
            ApplyTenantQueryFilter<ListingPricing>(modelBuilder);
            ApplyTenantQueryFilter<ListingDailyRate>(modelBuilder);
            ApplyTenantQueryFilter<ListingDailyInventory>(modelBuilder);
            ApplyTenantQueryFilter<AvailabilityBlock>(modelBuilder);
            ApplyTenantQueryFilter<MessageTemplate>(modelBuilder);
            ApplyTenantQueryFilter<CommunicationLog>(modelBuilder);
            ApplyTenantQueryFilter<OutboxMessage>(modelBuilder);
            ApplyTenantQueryFilter<AutomationSchedule>(modelBuilder);
            ApplyTenantQueryFilter<BankAccount>(modelBuilder);
            ApplyTenantQueryFilter<TenantPricingSetting>(modelBuilder);
            ApplyTenantQueryFilter<QuoteRedemption>(modelBuilder);
            ApplyTenantQueryFilter<WhatsAppInboundMessage>(modelBuilder);
            ApplyTenantQueryFilter<ConsumedEvent>(modelBuilder);
            ApplyTenantQueryFilter<HostKycDocument>(modelBuilder);
            ApplyTenantQueryFilter<PropertyComplianceProfile>(modelBuilder);
            ApplyTenantQueryFilter<OnboardingChecklistItem>(modelBuilder);
            ApplyTenantQueryFilter<AuditLog>(modelBuilder);
            ApplyTenantQueryFilter<PromoCode>(modelBuilder);
            ApplyTenantQueryFilter<ListingPricingRule>(modelBuilder);
            ApplyTenantQueryFilter<ChannelConfig>(modelBuilder);
            ApplyTenantQueryFilter<BookingInvoice>(modelBuilder);
            ApplyTenantQueryFilter<AddOnService>(modelBuilder);

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
            modelBuilder.Entity<Booking>()
                .Property(b => b.BaseAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.DiscountAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.ConvenienceFeeAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.FinalAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.PricingSource)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .HasDefaultValue("Public")
                .IsRequired();
            modelBuilder.Entity<Booking>()
                .Property(b => b.QuoteTokenNonce)
                .HasColumnType("varchar(50)")
                .HasMaxLength(50);
            modelBuilder.Entity<Booking>()
                .Property(b => b.QuoteExpiresAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.Property(p => p.Amount)
                    .HasPrecision(18, 2);

                entity.Property(p => p.BaseAmount)
                    .HasPrecision(18, 2);

                entity.Property(p => p.DiscountAmount)
                    .HasPrecision(18, 2);

                entity.Property(p => p.ConvenienceFeeAmount)
                    .HasPrecision(18, 2);

                entity.Property(p => p.Method)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(p => p.Type)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(p => p.Status)
                    .HasMaxLength(20)
                    .HasDefaultValue("pending");

                entity.Property(p => p.RazorpayOrderId)
                    .HasMaxLength(100);

                entity.Property(p => p.RazorpayPaymentId)
                    .HasMaxLength(100);

                entity.Property(p => p.RazorpaySignature)
                    .HasMaxLength(200);

                entity.Property(p => p.Note)
                    .IsRequired();

                entity.HasOne(p => p.Booking)
                    .WithMany(b => b.Payments)
                    .HasForeignKey(p => p.BookingId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

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
                .Property(r => r.Date)
                .HasColumnType("date");

            modelBuilder.Entity<ListingPricing>()
                .HasOne(p => p.Listing)
                .WithOne(l => l.Pricing)
                .HasForeignKey<ListingPricing>(p => p.ListingId)
                .OnDelete(deleteBehavior);

            // ListingDailyRate is related to Listing only (FK: ListingId). Do not infer a second relationship to ListingPricing
            // (which would create shadow column ListingPricingListingId that may not exist in the database).
            modelBuilder.Entity<ListingPricing>()
                .Ignore(p => p.DailyRates);

            modelBuilder.Entity<ListingDailyRate>()
                .HasOne(r => r.Listing)
                .WithMany(l => l.DailyRates)
                .HasForeignKey(r => r.ListingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<ListingDailyInventory>()
                .ToTable("ListingDailyInventory");

            modelBuilder.Entity<ListingDailyInventory>()
                .ToTable(t => t.HasCheckConstraint("CK_ListingDailyInventory_RoomsAvailable_NonNegative", "[RoomsAvailable] >= 0"));

            modelBuilder.Entity<ListingDailyInventory>()
                .Property(i => i.Date)
                .HasColumnType("date");

            modelBuilder.Entity<ListingDailyInventory>()
                .Property(i => i.RoomsAvailable)
                .HasColumnType("int");

            modelBuilder.Entity<ListingDailyInventory>()
                .Property(i => i.Source)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired();

            modelBuilder.Entity<ListingDailyInventory>()
                .Property(i => i.Reason)
                .HasMaxLength(200)
                .HasColumnType("varchar(200)");

            modelBuilder.Entity<ListingDailyInventory>()
                .Property(i => i.UpdatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<ListingDailyInventory>()
                .HasIndex(i => new { i.TenantId, i.ListingId, i.Date })
                .IsUnique();

            modelBuilder.Entity<ListingDailyInventory>()
                .HasOne(i => i.Listing)
                .WithMany(l => l.DailyInventories)
                .HasForeignKey(i => i.ListingId)
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
                .Property(ab => ab.Inventory)
                .HasColumnType("bit");

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

            modelBuilder.Entity<Tenant>()
                .ToTable("Tenants");

            modelBuilder.Entity<Tenant>()
                .Property(t => t.Name)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<Tenant>()
                .Property(t => t.Slug)
                .HasColumnType("varchar(50)")
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<Tenant>()
                .Property(t => t.IsActive)
                .HasDefaultValue(true);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.OwnerName)
                .HasColumnType("nvarchar(100)")
                .HasMaxLength(100)
                .HasDefaultValue("");

            modelBuilder.Entity<Tenant>()
                .Property(t => t.OwnerEmail)
                .HasColumnType("varchar(200)")
                .HasMaxLength(200)
                .HasDefaultValue("");

            modelBuilder.Entity<Tenant>()
                .Property(t => t.OwnerPhone)
                .HasColumnType("varchar(20)")
                .HasMaxLength(20)
                .HasDefaultValue("");

            modelBuilder.Entity<Tenant>()
                .Property(t => t.CustomDomain)
                .HasColumnType("varchar(500)")
                .HasMaxLength(500);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.LogoUrl)
                .HasColumnType("varchar(500)")
                .HasMaxLength(500);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.BrandColor)
                .HasColumnType("varchar(7)")
                .HasMaxLength(7);

            modelBuilder.Entity<Tenant>()
                .Property(t => t.Plan)
                .HasColumnType("varchar(20)")
                .HasMaxLength(20)
                .HasDefaultValue("free");

            modelBuilder.Entity<Tenant>()
                .Property(t => t.CreatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<Tenant>()
                .HasIndex(t => t.Slug)
                .IsUnique();

            // TenantProfile (1:1 with Tenant, keyed on TenantId)
            modelBuilder.Entity<TenantProfile>()
                .ToTable("TenantProfiles");
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.LegalName).HasMaxLength(200);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.DisplayName).HasMaxLength(200);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.BusinessType).HasColumnType("varchar(30)").HasMaxLength(30).HasDefaultValue("Individual");
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.Pincode).HasColumnType("varchar(10)").HasMaxLength(10);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.PanLast4).HasColumnType("varchar(4)").HasMaxLength(4);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.PanHash).HasColumnType("varchar(200)").HasMaxLength(200);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.Gstin).HasColumnType("varchar(15)").HasMaxLength(15);
            modelBuilder.Entity<TenantProfile>()
                .Property(p => p.OnboardingStatus).HasColumnType("varchar(30)").HasMaxLength(30).HasDefaultValue("Draft");
            modelBuilder.Entity<TenantProfile>()
                .HasOne(p => p.Tenant).WithOne().HasForeignKey<TenantProfile>(p => p.TenantId).OnDelete(DeleteBehavior.Cascade);

            // HostKycDocument
            modelBuilder.Entity<HostKycDocument>()
                .ToTable("HostKycDocuments");
            modelBuilder.Entity<HostKycDocument>()
                .Property(d => d.DocType).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            modelBuilder.Entity<HostKycDocument>()
                .Property(d => d.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("Pending");
            modelBuilder.Entity<HostKycDocument>()
                .HasIndex(d => new { d.TenantId, d.DocType });

            // PropertyComplianceProfile (1:1 with Property, keyed on PropertyId)
            modelBuilder.Entity<PropertyComplianceProfile>()
                .ToTable("PropertyComplianceProfiles");
            modelBuilder.Entity<PropertyComplianceProfile>()
                .Property(c => c.OwnershipType).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("Owner");
            modelBuilder.Entity<PropertyComplianceProfile>()
                .HasOne(c => c.Property).WithOne().HasForeignKey<PropertyComplianceProfile>(c => c.PropertyId).OnDelete(deleteBehavior);

            // OnboardingChecklistItem
            modelBuilder.Entity<OnboardingChecklistItem>()
                .ToTable("OnboardingChecklistItems");
            modelBuilder.Entity<OnboardingChecklistItem>()
                .Property(i => i.Key).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
            modelBuilder.Entity<OnboardingChecklistItem>()
                .Property(i => i.Stage).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("FastStart");
            modelBuilder.Entity<OnboardingChecklistItem>()
                .Property(i => i.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("Pending");
            modelBuilder.Entity<OnboardingChecklistItem>()
                .HasIndex(i => new { i.TenantId, i.Key }).IsUnique();

            // AuditLog (append-only)
            modelBuilder.Entity<AuditLog>()
                .ToTable("AuditLogs");
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.Action).HasColumnType("varchar(100)").HasMaxLength(100).IsRequired();
            modelBuilder.Entity<AuditLog>()
                .Property(a => a.TimestampUtc).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.TenantId, a.TimestampUtc });

            // ---------- Billing domain ----------

            modelBuilder.Entity<BillingPlan>(e =>
            {
                e.ToTable("BillingPlans");
                e.HasKey(p => p.Id);
                e.Property(p => p.Code).HasColumnType("varchar(30)").IsRequired();
                e.Property(p => p.Name).HasMaxLength(100).IsRequired();
                e.Property(p => p.MonthlyPriceInr).HasColumnType("decimal(18,2)");
                e.HasIndex(p => p.Code).IsUnique();
            });

            modelBuilder.Entity<TenantSubscription>(e =>
            {
                e.ToTable("TenantSubscriptions");
                e.Property(s => s.Status).HasColumnType("varchar(20)").HasDefaultValue(SubscriptionStatuses.Trial);
                e.Property(s => s.LockReason).HasColumnType("varchar(30)");
                e.HasOne(s => s.Tenant).WithMany().HasForeignKey(s => s.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(s => s.Plan).WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(s => s.TenantId);
            });

            modelBuilder.Entity<TenantCreditsLedger>(e =>
            {
                e.ToTable("TenantCreditsLedger");
                e.HasKey(l => l.Id);
                e.Property(l => l.Type).HasColumnType("varchar(20)").IsRequired();
                e.Property(l => l.Reason).HasColumnType("varchar(50)").IsRequired();
                e.Property(l => l.ReferenceType).HasColumnType("varchar(50)");
                e.Property(l => l.ReferenceId).HasColumnType("varchar(50)");
                e.Property(l => l.CreatedAtUtc).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasOne(l => l.Tenant).WithMany().HasForeignKey(l => l.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(l => l.TenantId);
            });

            modelBuilder.Entity<BillingInvoice>(e =>
            {
                e.ToTable("BillingInvoices");
                e.HasKey(i => i.Id);
                e.Property(i => i.Status).HasColumnType("varchar(20)").HasDefaultValue(InvoiceStatuses.Draft);
                e.Property(i => i.Provider).HasColumnType("varchar(20)");
                e.HasOne(i => i.Tenant).WithMany().HasForeignKey(i => i.TenantId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(i => new { i.TenantId, i.Status });
            });

            modelBuilder.Entity<BillingPayment>(e =>
            {
                e.ToTable("BillingPayments");
                e.HasKey(p => p.Id);
                e.Property(p => p.Status).HasColumnType("varchar(20)");
                e.Property(p => p.CreatedAtUtc).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasOne(p => p.Invoice).WithMany().HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ListingPhoto>(e =>
            {
                e.ToTable("ListingPhotos");
                e.HasKey(p => p.Id);
                e.Property(p => p.Url).HasColumnType("nvarchar(1000)");
                e.Property(p => p.OriginalFileName).HasColumnType("nvarchar(200)");
                e.Property(p => p.ContentType).HasColumnType("varchar(20)");
                e.Property(p => p.Caption).HasColumnType("nvarchar(300)");
                e.Property(p => p.CreatedAtUtc).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.Property(p => p.UpdatedAtUtc).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasOne(p => p.Listing).WithMany(l => l.Photos).HasForeignKey(p => p.ListingId).OnDelete(DeleteBehavior.Cascade);
            });

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
                .HasIndex(cl => new { cl.TenantId, cl.IdempotencyKey })
                .HasDatabaseName("IX_CommunicationLog_TenantId_IdempotencyKey")
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
                .Property(o => o.Topic)
                .HasMaxLength(80)
                .HasColumnType("varchar(80)")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.EventType)
                .HasMaxLength(80)
                .HasColumnType("varchar(80)")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.Status)
                .HasMaxLength(20)
                .HasColumnType("varchar(20)")
                .IsRequired()
                .HasDefaultValue("Pending");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.CorrelationId)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.EntityId)
                .HasMaxLength(100)
                .HasColumnType("varchar(100)");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.OccurredUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.NextAttemptUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.PayloadJson)
                .HasColumnType("text")
                .IsRequired();

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.AggregateType)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.AggregateId)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.HeadersJson)
                .HasColumnType("text");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.CreatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.UpdatedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.PublishedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<OutboxMessage>()
                .Property(o => o.LastError)
                .HasColumnType("text");

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => new { o.Status, o.NextAttemptUtc })
                .HasDatabaseName("IX_OutboxMessage_Status_NextAttemptUtc");

            modelBuilder.Entity<OutboxMessage>()
                .HasIndex(o => new { o.TenantId, o.CreatedAtUtc })
                .HasDatabaseName("IX_OutboxMessage_TenantId_CreatedAtUtc");

            modelBuilder.Entity<ConsumedEvent>()
                .ToTable("ConsumedEvent");

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.Id)
                .HasColumnType("bigint");

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.ConsumerName)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.EventId)
                .HasColumnType("varchar(150)")
                .HasMaxLength(150)
                .IsRequired();

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.EventType)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.ProcessedAtUtc)
                .HasColumnType("datetime")
                .IsRequired();

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.PayloadHash)
                .HasColumnType("varchar(128)")
                .HasMaxLength(128);

            modelBuilder.Entity<ConsumedEvent>()
                .Property(x => x.Status)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30);

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

            modelBuilder.Entity<AutomationSchedule>()
                .HasOne(a => a.Booking)
                .WithMany()
                .HasForeignKey(a => a.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<TenantPricingSetting>()
                .ToTable("TenantPricingSettings");

            modelBuilder.Entity<TenantPricingSetting>()
                .HasKey(x => x.TenantId);

            modelBuilder.Entity<TenantPricingSetting>()
                .Property(x => x.ConvenienceFeePercent)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(3.00m);

            modelBuilder.Entity<TenantPricingSetting>()
                .Property(x => x.GlobalDiscountPercent)
                .HasColumnType("decimal(5,2)")
                .HasDefaultValue(0.00m);

            modelBuilder.Entity<TenantPricingSetting>()
                .Property(x => x.UpdatedAtUtc)
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETUTCDATE()");

            modelBuilder.Entity<TenantPricingSetting>()
                .Property(x => x.UpdatedBy)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            modelBuilder.Entity<QuoteRedemption>()
                .ToTable("QuoteRedemption");

            modelBuilder.Entity<QuoteRedemption>()
                .Property(x => x.Id)
                .HasColumnType("bigint");

            modelBuilder.Entity<QuoteRedemption>()
                .Property(x => x.Nonce)
                .HasColumnType("varchar(50)")
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<QuoteRedemption>()
                .Property(x => x.RedeemedAtUtc)
                .HasColumnType("datetime");

            modelBuilder.Entity<QuoteRedemption>()
                .HasOne(x => x.Booking)
                .WithMany()
                .HasForeignKey(x => x.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .ToTable("WhatsAppInboundMessage");

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.Id)
                .HasColumnType("bigint");

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.Provider)
                .HasColumnType("varchar(50)")
                .HasMaxLength(50)
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.ProviderMessageId)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100)
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.FromNumber)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.ToNumber)
                .HasColumnType("varchar(30)")
                .HasMaxLength(30)
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.ReceivedAtUtc)
                .HasColumnType("datetime")
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.PayloadJson)
                .HasColumnType("text")
                .IsRequired();

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .Property(x => x.CorrelationId)
                .HasColumnType("varchar(100)")
                .HasMaxLength(100);

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .HasOne(x => x.Booking)
                .WithMany()
                .HasForeignKey(x => x.BookingId)
                .OnDelete(deleteBehavior);

            modelBuilder.Entity<WhatsAppInboundMessage>()
                .HasOne(x => x.Guest)
                .WithMany()
                .HasForeignKey(x => x.GuestId)
                .OnDelete(deleteBehavior);

            // ---------- PromoCode ----------
            modelBuilder.Entity<PromoCode>(e =>
            {
                e.ToTable("PromoCodes");
                e.HasKey(p => p.Id);
                e.Property(p => p.Code).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
                e.Property(p => p.DiscountType).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("Percent");
                e.Property(p => p.DiscountValue).HasColumnType("decimal(18,2)");
                e.Property(p => p.ValidFrom).HasColumnType("datetime");
                e.Property(p => p.ValidTo).HasColumnType("datetime");
                e.Property(p => p.TimesUsed).HasDefaultValue(0);
                e.Property(p => p.IsActive).HasDefaultValue(true);
                e.Property(p => p.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
            });

            // ---------- Review (no tenant filter — scoped via Booking FK) ----------
            modelBuilder.Entity<Review>(e =>
            {
                e.ToTable("Reviews");
                e.HasKey(r => r.Id);
                e.Property(r => r.Title).HasMaxLength(200);
                e.Property(r => r.Body).HasMaxLength(2000);
                e.Property(r => r.HostResponse).HasMaxLength(2000);
                e.Property(r => r.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.Property(r => r.HostResponseAt).HasColumnType("datetime");

                e.HasOne(r => r.Booking).WithMany().HasForeignKey(r => r.BookingId).OnDelete(deleteBehavior);
                e.HasOne(r => r.Guest).WithMany().HasForeignKey(r => r.GuestId).OnDelete(deleteBehavior);
                e.HasOne(r => r.Listing).WithMany().HasForeignKey(r => r.ListingId).OnDelete(deleteBehavior);

                e.HasIndex(r => r.BookingId).IsUnique();
                e.HasIndex(r => r.ListingId);
            });

            // ---------- ChannelConfig ----------
            modelBuilder.Entity<ChannelConfig>(e =>
            {
                e.ToTable("ChannelConfigs");
                e.HasKey(c => c.Id);
                e.Property(c => c.Provider).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired().HasDefaultValue("channex");
                e.Property(c => c.ApiKey).HasColumnType("nvarchar(500)").HasMaxLength(500);
                e.Property(c => c.ExternalPropertyId).HasColumnType("varchar(200)").HasMaxLength(200);
                e.Property(c => c.IsConnected).HasDefaultValue(false);
                e.Property(c => c.LastSyncAt).HasColumnType("datetime");
                e.Property(c => c.LastSyncError).HasColumnType("nvarchar(500)").HasMaxLength(500);
                e.Property(c => c.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasOne(c => c.Property).WithMany().HasForeignKey(c => c.PropertyId).OnDelete(deleteBehavior);
                e.HasIndex(c => new { c.TenantId, c.PropertyId, c.Provider }).IsUnique();
            });

            // ---------- ListingPricingRule ----------
            modelBuilder.Entity<ListingPricingRule>(e =>
            {
                e.ToTable("ListingPricingRules");
                e.HasKey(r => r.Id);
                e.Property(r => r.RuleType).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("LOS");
                e.Property(r => r.DiscountPercent).HasColumnType("decimal(5,2)");
                e.Property(r => r.SeasonStart).HasColumnType("date");
                e.Property(r => r.SeasonEnd).HasColumnType("date");
                e.Property(r => r.Label).HasMaxLength(100);
                e.Property(r => r.IsActive).HasDefaultValue(true);
                e.Property(r => r.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.HasOne(r => r.Listing).WithMany(l => l.PricingRules).HasForeignKey(r => r.ListingId).OnDelete(deleteBehavior);
                e.HasIndex(r => new { r.TenantId, r.ListingId, r.RuleType });
            });

            // ---------- BookingInvoice ----------
            modelBuilder.Entity<BookingInvoice>(e =>
            {
                e.ToTable("BookingInvoices");
                e.HasKey(i => i.Id);
                e.Property(i => i.InvoiceNumber).HasColumnType("varchar(50)").HasMaxLength(50).IsRequired();
                e.Property(i => i.GuestName).HasMaxLength(200);
                e.Property(i => i.GuestEmail).HasMaxLength(200);
                e.Property(i => i.GuestPhone).HasColumnType("varchar(20)").HasMaxLength(20);
                e.Property(i => i.PropertyName).HasMaxLength(200);
                e.Property(i => i.ListingName).HasMaxLength(200);
                e.Property(i => i.BaseAmount).HasColumnType("decimal(18,2)");
                e.Property(i => i.GstRate).HasColumnType("decimal(5,4)");
                e.Property(i => i.GstAmount).HasColumnType("decimal(18,2)");
                e.Property(i => i.TotalAmount).HasColumnType("decimal(18,2)");
                e.Property(i => i.SupplierGstin).HasColumnType("varchar(15)").HasMaxLength(15);
                e.Property(i => i.SupplierLegalName).HasMaxLength(200);
                e.Property(i => i.SupplierAddress).HasMaxLength(500);
                e.Property(i => i.PlaceOfSupply).HasColumnType("varchar(50)").HasMaxLength(50);
                e.Property(i => i.GeneratedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
                e.Property(i => i.Status).HasColumnType("varchar(20)").HasMaxLength(20).HasDefaultValue("generated");
                e.HasOne(i => i.Booking).WithMany().HasForeignKey(i => i.BookingId).OnDelete(deleteBehavior);
                e.HasIndex(i => i.BookingId).IsUnique();
                e.HasIndex(i => i.InvoiceNumber).IsUnique();
            });

            // ---------- AddOnService ----------
            modelBuilder.Entity<AddOnService>(e =>
            {
                e.ToTable("AddOnServices");
                e.HasKey(a => a.Id);
                e.Property(a => a.Name).HasMaxLength(100).IsRequired();
                e.Property(a => a.Description).HasMaxLength(500);
                e.Property(a => a.Price).HasColumnType("decimal(18,2)");
                e.Property(a => a.PriceType).HasColumnType("varchar(20)").HasMaxLength(20).IsRequired().HasDefaultValue("per_booking");
                e.Property(a => a.Category).HasColumnType("varchar(50)").HasMaxLength(50);
                e.Property(a => a.IsActive).HasDefaultValue(true);
                e.Property(a => a.CreatedAt).HasColumnType("datetime").HasDefaultValueSql("GETUTCDATE()");
            });

            // ---------- ListingAddOn (join table) ----------
            modelBuilder.Entity<ListingAddOn>(e =>
            {
                e.ToTable("ListingAddOns");
                e.HasKey(la => la.Id);
                e.Property(la => la.IsEnabled).HasDefaultValue(true);
                e.Property(la => la.OverridePrice).HasColumnType("decimal(18,2)");
                e.HasOne(la => la.Listing).WithMany().HasForeignKey(la => la.ListingId).OnDelete(deleteBehavior);
                e.HasOne(la => la.AddOnService).WithMany().HasForeignKey(la => la.AddOnServiceId).OnDelete(deleteBehavior);
                e.HasIndex(la => new { la.ListingId, la.AddOnServiceId }).IsUnique();
            });

            ConfigureTenantOwnership(modelBuilder, deleteBehavior);
        }

        private static void ConfigureTenantOwnership(ModelBuilder modelBuilder, DeleteBehavior deleteBehavior)
        {
            ConfigureTenantOwnedEntity<Property>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<Listing>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<Booking>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<Guest>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<Payment>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<User>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ListingPricing>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ListingDailyRate>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ListingDailyInventory>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<AvailabilityBlock>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<MessageTemplate>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<CommunicationLog>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<OutboxMessage>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<AutomationSchedule>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<BankAccount>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<TenantPricingSetting>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<QuoteRedemption>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<WhatsAppInboundMessage>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ConsumedEvent>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<PromoCode>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ListingPricingRule>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<ChannelConfig>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<BookingInvoice>(modelBuilder, deleteBehavior);
            ConfigureTenantOwnedEntity<AddOnService>(modelBuilder, deleteBehavior);

            modelBuilder.Entity<Listing>().HasIndex(x => new { x.TenantId, x.PropertyId });
            modelBuilder.Entity<Booking>().HasIndex(x => new { x.TenantId, x.ListingId });
            modelBuilder.Entity<Payment>().HasIndex(x => new { x.TenantId, x.BookingId });
            modelBuilder.Entity<Payment>().HasIndex(x => x.RazorpayOrderId)
                .IsUnique()
                .HasFilter("[RazorpayOrderId] IS NOT NULL")
                .HasDatabaseName("IX_Payments_RazorpayOrderId_Unique");
            modelBuilder.Entity<ListingPricing>().HasIndex(x => new { x.TenantId, x.ListingId }).IsUnique();
            modelBuilder.Entity<ListingDailyRate>().HasIndex(x => new { x.TenantId, x.ListingId, x.Date }).IsUnique();
            modelBuilder.Entity<ListingDailyInventory>().HasIndex(x => new { x.TenantId, x.ListingId, x.Date }).IsUnique();
            modelBuilder.Entity<AvailabilityBlock>().HasIndex(x => new { x.TenantId, x.ListingId, x.StartDate, x.EndDate });
            modelBuilder.Entity<MessageTemplate>().HasIndex(x => new { x.TenantId, x.EventType, x.Channel });
            modelBuilder.Entity<CommunicationLog>().HasIndex(x => new { x.TenantId, x.BookingId });
            modelBuilder.Entity<AutomationSchedule>().HasIndex(x => new { x.TenantId, x.BookingId, x.EventType, x.DueAtUtc }).IsUnique();
            modelBuilder.Entity<BankAccount>().HasIndex(x => new { x.TenantId, x.AccountNumber });
            modelBuilder.Entity<TenantPricingSetting>().HasIndex(x => x.TenantId).IsUnique();
            modelBuilder.Entity<QuoteRedemption>().HasIndex(x => new { x.TenantId, x.Nonce }).IsUnique();
            modelBuilder.Entity<WhatsAppInboundMessage>().HasIndex(x => new { x.TenantId, x.Provider, x.ProviderMessageId }).IsUnique();
            modelBuilder.Entity<WhatsAppInboundMessage>().HasIndex(x => new { x.TenantId, x.ReceivedAtUtc });
            modelBuilder.Entity<ConsumedEvent>().HasIndex(x => new { x.TenantId, x.ConsumerName, x.EventId }).IsUnique();
            modelBuilder.Entity<ConsumedEvent>().HasIndex(x => new { x.TenantId, x.ProcessedAtUtc });
        }

        private static void ConfigureTenantOwnedEntity<TEntity>(ModelBuilder modelBuilder, DeleteBehavior deleteBehavior)
            where TEntity : class, ITenantOwnedEntity
        {
            modelBuilder.Entity<TEntity>()
                .Property(x => x.TenantId)
                .IsRequired();

            modelBuilder.Entity<TEntity>()
                .HasIndex(x => x.TenantId);

            modelBuilder.Entity<TEntity>()
                .HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(deleteBehavior);
        }

        public override int SaveChanges()
        {
            ApplyTenantOwnershipRules();
            ApplyAuditTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyTenantOwnershipRules();
            ApplyAuditTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyAuditTimestamps()
        {
            var utcNow = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added && entry.Entity.CreatedAtUtc == default)
                    entry.Entity.CreatedAtUtc = utcNow;
                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                    entry.Entity.UpdatedAtUtc = utcNow;
            }
        }

        private void ApplyTenantOwnershipRules()
        {
            var tenantId = GetResolvedTenantId();

            foreach (var entry in ChangeTracker.Entries<ITenantOwnedEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    if (entry.Entity.TenantId == 0)
                        entry.Entity.TenantId = tenantId;
                }
                else if (entry.State == EntityState.Modified)
                {
                    if (entry.Entity.TenantId != tenantId)
                        throw new InvalidOperationException("Tenant mismatch detected for a tenant-owned entity.");

                    entry.Property(nameof(ITenantOwnedEntity.TenantId)).IsModified = false;
                }
            }
        }

        private int GetResolvedTenantId()
        {
            return _tenantContextAccessor?.TenantId ?? FallbackTenantId;
        }

        private void ApplyTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ITenantOwnedEntity
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(CreateTenantFilterExpression<TEntity>());
        }

        private Expression<Func<TEntity, bool>> CreateTenantFilterExpression<TEntity>()
            where TEntity : class, ITenantOwnedEntity
        {
            return entity => entity.TenantId == GetResolvedTenantId();
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
        public DbSet<ListingDailyInventory> ListingDailyInventories { get; set; }
        public DbSet<MessageTemplate> MessageTemplates { get; set; }
        public DbSet<CommunicationLog> CommunicationLogs { get; set; }
        public DbSet<OutboxMessage> OutboxMessages { get; set; }
        public DbSet<AutomationSchedule> AutomationSchedules { get; set; }
        public DbSet<TenantPricingSetting> TenantPricingSettings { get; set; }
        public DbSet<QuoteRedemption> QuoteRedemptions { get; set; }
        public DbSet<WhatsAppInboundMessage> WhatsAppInboundMessages { get; set; }
        public DbSet<ConsumedEvent> ConsumedEvents { get; set; }
        public DbSet<EnvironmentMarker> EnvironmentMarkers { get; set; }
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<TenantProfile> TenantProfiles { get; set; }
        public DbSet<HostKycDocument> HostKycDocuments { get; set; }
        public DbSet<PropertyComplianceProfile> PropertyComplianceProfiles { get; set; }
        public DbSet<OnboardingChecklistItem> OnboardingChecklistItems { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<BillingPlan> BillingPlans { get; set; }
        public DbSet<TenantSubscription> TenantSubscriptions { get; set; }
        public DbSet<TenantCreditsLedger> TenantCreditsLedger { get; set; }
        public DbSet<BillingInvoice> BillingInvoices { get; set; }
        public DbSet<BillingPayment> BillingPayments { get; set; }
        public DbSet<ListingPhoto> ListingPhotos { get; set; }
        public DbSet<PromoCode> PromoCodes { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<ListingPricingRule> ListingPricingRules { get; set; }
        public DbSet<ChannelConfig> ChannelConfigs { get; set; }
        public DbSet<BookingInvoice> BookingInvoices { get; set; }
        public DbSet<AddOnService> AddOnServices { get; set; }
        public DbSet<ListingAddOn> ListingAddOns { get; set; }
    }
}
