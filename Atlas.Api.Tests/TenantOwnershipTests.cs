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
    public async Task WhatsAppInboundMessage_QueryFilter_OnlyReturnsRowsForCurrentTenant()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        await using (var tenantOneContext = new AppDbContext(options, new TestTenantContextAccessor(1)))
        {
            tenantOneContext.WhatsAppInboundMessages.Add(new WhatsAppInboundMessage
            {
                Provider = "Meta",
                ProviderMessageId = "m-1",
                FromNumber = "+15550001",
                ToNumber = "+15550999",
                ReceivedAtUtc = DateTime.UtcNow,
                PayloadJson = "{}"
            });
            await tenantOneContext.SaveChangesAsync();
        }

        await using (var tenantTwoContext = new AppDbContext(options, new TestTenantContextAccessor(2)))
        {
            tenantTwoContext.WhatsAppInboundMessages.Add(new WhatsAppInboundMessage
            {
                Provider = "Meta",
                ProviderMessageId = "m-2",
                FromNumber = "+15550002",
                ToNumber = "+15550999",
                ReceivedAtUtc = DateTime.UtcNow,
                PayloadJson = "{}"
            });
            await tenantTwoContext.SaveChangesAsync();
        }

        await using var filteredContext = new AppDbContext(options, new TestTenantContextAccessor(1));
        var rows = await filteredContext.WhatsAppInboundMessages.AsNoTracking().ToListAsync();

        Assert.Single(rows);
        Assert.Equal("m-1", rows[0].ProviderMessageId);
        Assert.Equal(1, rows[0].TenantId);
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
