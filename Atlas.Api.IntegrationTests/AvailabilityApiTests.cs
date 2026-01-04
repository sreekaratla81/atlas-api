using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class AvailabilityApiTests : IntegrationTestBase
{
    public AvailabilityApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_ReturnsAvailableListingsWithPricing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var blockedListing = await DataSeeder.SeedListingAsync(db, property);

        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, null, null, "USD");
        await DataSeeder.SeedListingPricingAsync(db, blockedListing, 120m, null, null, "USD");
        await DataSeeder.SeedListingDailyRateAsync(db, listing, new DateTime(2025, 1, 2), 150m, "USD");

        db.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = blockedListing.Id,
            StartDate = new DateTime(2025, 1, 1),
            EndDate = new DateTime(2025, 1, 3),
            BlockType = "Booking",
            Source = "System",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync("/api/availability?propertyId=" + property.Id + "&checkIn=2025-01-01&checkOut=2025-01-03&guests=1");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AvailabilityResponseDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.IsGenericAvailable);
        Assert.Single(payload.Listings);

        var listingResult = payload.Listings.Single();
        Assert.Equal(listing.Id, listingResult.ListingId);
        Assert.Equal("USD", listingResult.Currency);
        Assert.Equal(2, listingResult.NightlyRates.Count);
        Assert.Equal(250m, listingResult.TotalPrice);

        await transaction.RollbackAsync();
    }
}
