using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class AvailabilityApiTests : IntegrationTestBase
{
    public AvailabilityApiTests(CustomWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task Get_ReturnsAvailableListingsWithPricing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        var blockedListing = await DataSeeder.SeedListingAsync(db, property);

        await DataSeeder.SeedListingBasePriceAsync(db, listing, 100m, "USD");
        await DataSeeder.SeedListingBasePriceAsync(db, blockedListing, 120m, "USD");
        await DataSeeder.SeedListingDailyOverrideAsync(db, listing, new DateTime(2025, 1, 2), 150m);

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
    }
}
