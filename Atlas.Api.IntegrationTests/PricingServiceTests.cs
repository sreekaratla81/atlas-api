using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace Atlas.Api.IntegrationTests;

public class PricingServiceTests : IntegrationTestBase
{
    public PricingServiceTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task GetPricingAsync_UsesOverridesAndTotalsRates()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 110m, 140m, null, "USD");
        await DataSeeder.SeedListingDailyRateAsync(db, listing, new DateTime(2025, 2, 8), 200m, "USD");

        var service = new PricingService(db, new StubTenantPricingSettingsService(), NullLogger<PricingService>.Instance);

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 2, 7), new DateTime(2025, 2, 10));

        Assert.Equal(3, result.NightlyRates.Count);
        Assert.Equal(new[] { 140m, 200m, 110m }, result.NightlyRates.Select(rate => rate.Rate));
        Assert.Equal(450m, result.TotalPrice);

        await transaction.RollbackAsync();
    }
}


internal sealed class StubTenantPricingSettingsService : ITenantPricingSettingsService
{
    public Task<TenantPricingSetting> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TenantPricingSetting { ConvenienceFeePercent = 3m, GlobalDiscountPercent = 0m });

    public void Invalidate(int tenantId) { }

    public Task<TenantPricingSetting> UpdateCurrentAsync(UpdateTenantPricingSettingsDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new TenantPricingSetting { ConvenienceFeePercent = request.ConvenienceFeePercent, GlobalDiscountPercent = request.GlobalDiscountPercent });
}
