using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class TenantOwnershipTests
{
    [Fact]
    public async Task SaveChanges_AssignsTenantId_FromTenantContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new AppDbContext(options, new TestTenantContextAccessor(7));
        context.Properties.Add(new Property
        {
            Name = "P1",
            Address = "Addr",
            Type = "Villa",
            OwnerName = "Owner",
            ContactPhone = "123",
            Status = "Active"
        });

        await context.SaveChangesAsync();

        var property = await context.Properties.AsNoTracking().SingleAsync();
        Assert.Equal(7, property.TenantId);
    }

    [Fact]
    public async Task QueryFilter_OnlyReturnsRowsForCurrentTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var seedContext = new AppDbContext(options, new TestTenantContextAccessor(1)))
        {
            seedContext.Properties.Add(new Property
            {
                Name = "Tenant1",
                Address = "Addr1",
                Type = "Villa",
                OwnerName = "Owner1",
                ContactPhone = "111",
                Status = "Active"
            });
            await seedContext.SaveChangesAsync();
        }

        await using (var seedContext = new AppDbContext(options, new TestTenantContextAccessor(2)))
        {
            seedContext.Properties.Add(new Property
            {
                Name = "Tenant2",
                Address = "Addr2",
                Type = "Villa",
                OwnerName = "Owner2",
                ContactPhone = "222",
                Status = "Active"
            });
            await seedContext.SaveChangesAsync();
        }

        await using var tenantOneContext = new AppDbContext(options, new TestTenantContextAccessor(1));
        var tenantOneRows = await tenantOneContext.Properties.AsNoTracking().ToListAsync();

        Assert.Single(tenantOneRows);
        Assert.Equal("Tenant1", tenantOneRows[0].Name);
    }


    [Fact]
    public async Task SaveChanges_AssignsTenantId_ForConsumedEvent_FromTenantContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new AppDbContext(options, new TestTenantContextAccessor(9));
        context.ConsumedEvents.Add(new ConsumedEvent
        {
            ConsumerName = "booking-consumer",
            EventId = "evt-1",
            EventType = "booking.confirmed",
            ProcessedAtUtc = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        var consumedEvent = await context.ConsumedEvents.AsNoTracking().SingleAsync();
        Assert.Equal(9, consumedEvent.TenantId);
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
