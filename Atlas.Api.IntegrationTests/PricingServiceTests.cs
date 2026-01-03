using Atlas.Api.Data;
using Atlas.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace Atlas.Api.IntegrationTests;

public class PricingServiceTests : IntegrationTestBase
{
    public PricingServiceTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetPricingAsync_UsesOverridesAndTotalsRates()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 110m, 140m, "USD");
        await DataSeeder.SeedListingDailyRateAsync(db, listing, new DateTime(2025, 2, 8), 200m);

        var service = new PricingService(db);

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 2, 7), new DateTime(2025, 2, 10));

        Assert.Equal(3, result.NightlyRates.Count);
        Assert.Equal(new[] { 110m, 200m, 140m }, result.NightlyRates.Select(rate => rate.Rate));
        Assert.Equal(450m, result.TotalPrice);

        await transaction.RollbackAsync();
    }
}
