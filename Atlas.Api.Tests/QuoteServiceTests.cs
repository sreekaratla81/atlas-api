using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Tests;

public class QuoteServiceTests
{
    [Fact]
    public async Task ValidateAsync_Fails_WhenTenantDoesNotMatchTokenTenant()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await SeedTenantDataAsync(dbName, 1);

        var issuer = BuildQuoteService(dbName, 1);
        var issued = await issuer.IssueAsync(new CreateQuoteRequestDto
        {
            ListingId = 1,
            CheckIn = new DateTime(2025, 03, 01),
            CheckOut = new DateTime(2025, 03, 03),
            Guests = 2,
            QuotedBaseAmount = 1000,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });

        var validator = BuildQuoteService(dbName, 2);
        var validation = await validator.ValidateAsync(issued.Token);

        Assert.False(validation.IsValid);
        Assert.Contains("tenant mismatch", validation.Error!, StringComparison.OrdinalIgnoreCase);
    }

    private static QuoteService BuildQuoteService(string dbName, int tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var tenantAccessor = new TestTenantContextAccessor(tenantId);
        var db = new AppDbContext(options, tenantAccessor);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var tenantSettingsService = new TenantPricingSettingsService(db, cache, tenantAccessor);
        var pricingService = new PricingService(db, tenantSettingsService);

        return new QuoteService(
            tenantAccessor,
            tenantSettingsService,
            pricingService,
            db,
            Options.Create(new QuoteOptions { SigningKey = "test-signing-key" }));
    }

    private static async Task SeedTenantDataAsync(string dbName, int tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        var db = new AppDbContext(options, new TestTenantContextAccessor(tenantId));

        if (!await db.Tenants.AnyAsync(t => t.Id == tenantId))
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = $"Tenant {tenantId}", Slug = $"t{tenantId}", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        }

        if (!await db.Properties.AnyAsync())
        {
            var property = new Property { Name = "P", Address = "A", Type = "Villa", OwnerName = "O", ContactPhone = "1", CommissionPercent = 10, Status = "Active" };
            db.Properties.Add(property);
            await db.SaveChangesAsync();
            var listing = new Listing { Id = 1, PropertyId = property.Id, Property = property, Name = "L", Floor = 1, Type = "Room", Status = "Available", WifiName = "w", WifiPassword = "p", MaxGuests = 2 };
            db.Listings.Add(listing);
            db.ListingPricings.Add(new ListingPricing { ListingId = 1, BaseNightlyRate = 500, WeekendNightlyRate = 500, Currency = "INR" });
        }

        await db.SaveChangesAsync();
    }

    private sealed class TestTenantContextAccessor : ITenantContextAccessor
    {
        public TestTenantContextAccessor(int tenantId) => TenantId = tenantId;
        public int? TenantId { get; }
    }
}
