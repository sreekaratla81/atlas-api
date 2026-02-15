using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class TenantScopedWorkflowSchemaIntegrationTests : IntegrationTestBase
{
    public TenantScopedWorkflowSchemaIntegrationTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public async Task CommunicationLog_IdempotencyKeyUniquePerTenant_CrossTenantReuseAllowed()
    {
        const string idempotencyKey = "workflow-confirmation-001";

        await using (var tenantOneDb = CreateTenantContext(1))
        {
            tenantOneDb.CommunicationLogs.Add(CreateCommunicationLog(idempotencyKey));
            await tenantOneDb.SaveChangesAsync();

            tenantOneDb.CommunicationLogs.Add(CreateCommunicationLog(idempotencyKey));
            await Assert.ThrowsAsync<DbUpdateException>(() => tenantOneDb.SaveChangesAsync());
        }

        await using (var tenantTwoDb = CreateTenantContext(2))
        {
            tenantTwoDb.CommunicationLogs.Add(CreateCommunicationLog(idempotencyKey));
            await tenantTwoDb.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task AutomationSchedule_DuplicateTenantBookingEventDueAt_ThrowsUniqueConstraintViolation()
    {
        await using var db = CreateTenantContext(1);

        var booking = await CreateBookingAsync(db, "tenant-one");
        var dueAtUtc = new DateTime(2026, 2, 10, 9, 0, 0, DateTimeKind.Utc);

        db.AutomationSchedules.Add(new AutomationSchedule
        {
            BookingId = booking.Id,
            EventType = "BookingConfirmed",
            DueAtUtc = dueAtUtc,
            Status = "Pending"
        });
        await db.SaveChangesAsync();

        db.AutomationSchedules.Add(new AutomationSchedule
        {
            BookingId = booking.Id,
            EventType = "BookingConfirmed",
            DueAtUtc = dueAtUtc,
            Status = "Pending"
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task PendingMigrations_RemainEmptyAfterTestHarnessInitialization()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingMigrations = db.Database.GetPendingMigrations();

        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task TenantQueryFilters_IsolateCommunicationLogAndAutomationScheduleRows()
    {
        await using (var tenantOneDb = CreateTenantContext(1))
        {
            var tenantOneBooking = await CreateBookingAsync(tenantOneDb, "tenant-one-filter");
            tenantOneDb.CommunicationLogs.Add(CreateCommunicationLog("tenant-one-key"));
            tenantOneDb.AutomationSchedules.Add(new AutomationSchedule
            {
                BookingId = tenantOneBooking.Id,
                EventType = "BookingConfirmed",
                DueAtUtc = new DateTime(2026, 2, 11, 8, 0, 0, DateTimeKind.Utc),
                Status = "Pending"
            });
            await tenantOneDb.SaveChangesAsync();
        }

        await using (var tenantTwoDb = CreateTenantContext(2))
        {
            var tenantTwoBooking = await CreateBookingAsync(tenantTwoDb, "tenant-two-filter");
            tenantTwoDb.CommunicationLogs.Add(CreateCommunicationLog("tenant-two-key"));
            tenantTwoDb.AutomationSchedules.Add(new AutomationSchedule
            {
                BookingId = tenantTwoBooking.Id,
                EventType = "BookingConfirmed",
                DueAtUtc = new DateTime(2026, 2, 11, 9, 0, 0, DateTimeKind.Utc),
                Status = "Pending"
            });
            await tenantTwoDb.SaveChangesAsync();
        }

        await using var tenantOneReadDb = CreateTenantContext(1);
        var tenantOneLogs = await tenantOneReadDb.CommunicationLogs.AsNoTracking().ToListAsync();
        var tenantOneSchedules = await tenantOneReadDb.AutomationSchedules.AsNoTracking().ToListAsync();

        Assert.Single(tenantOneLogs);
        Assert.Equal("tenant-one-key", tenantOneLogs[0].IdempotencyKey);

        Assert.Single(tenantOneSchedules);
        Assert.Equal(1, tenantOneSchedules[0].TenantId);

        await using var tenantTwoReadDb = CreateTenantContext(2);
        var tenantTwoLogs = await tenantTwoReadDb.CommunicationLogs.AsNoTracking().ToListAsync();
        var tenantTwoSchedules = await tenantTwoReadDb.AutomationSchedules.AsNoTracking().ToListAsync();

        Assert.Single(tenantTwoLogs);
        Assert.Equal("tenant-two-key", tenantTwoLogs[0].IdempotencyKey);

        Assert.Single(tenantTwoSchedules);
        Assert.Equal(2, tenantTwoSchedules[0].TenantId);
    }

    private static CommunicationLog CreateCommunicationLog(string idempotencyKey)
    {
        return new CommunicationLog
        {
            Channel = "Email",
            EventType = "BookingConfirmed",
            ToAddress = "guest@example.com",
            CorrelationId = Guid.NewGuid().ToString("N"),
            IdempotencyKey = idempotencyKey,
            Provider = "TestProvider",
            Status = "Queued",
            TemplateVersion = 1
        };
    }

    private static async Task<Booking> CreateBookingAsync(AppDbContext db, string suffix)
    {
        var property = new Property
        {
            Name = $"Property-{suffix}",
            Address = "Integration Test Address",
            Type = "Apartment",
            OwnerName = "Owner",
            ContactPhone = "1234567890",
            Status = "Active"
        };

        var guest = new Guest
        {
            Name = $"Guest-{suffix}",
            Phone = "1234567890",
            Email = $"{suffix}@example.com"
        };

        db.Properties.Add(property);
        db.Guests.Add(guest);
        await db.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = $"Listing-{suffix}",
            Floor = 1,
            Type = "Studio",
            Status = "Active",
            WifiName = "wifi",
            WifiPassword = "password",
            MaxGuests = 2
        };

        db.Listings.Add(listing);
        await db.SaveChangesAsync();

        var booking = new Booking
        {
            ListingId = listing.Id,
            Listing = listing,
            GuestId = guest.Id,
            Guest = guest,
            CheckinDate = new DateTime(2026, 2, 15),
            CheckoutDate = new DateTime(2026, 2, 16),
            Notes = "Integration booking",
            PaymentStatus = "Paid"
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync();

        return booking;
    }

    private static AppDbContext CreateTenantContext(int tenantId)
    {
        var connectionString = Environment.GetEnvironmentVariable("Atlas_TestDb")
            ?? throw new InvalidOperationException("Atlas_TestDb must be configured for integration tests.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options, new TestTenantContextAccessor(tenantId));
    }

    private sealed class TestTenantContextAccessor : ITenantContextAccessor
    {
        public TestTenantContextAccessor(int tenantId)
        {
            TenantId = tenantId;
        }

        public int? TenantId { get; }
    }
}
