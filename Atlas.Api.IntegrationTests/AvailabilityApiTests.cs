using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class AvailabilityApiTests : IntegrationTestBase
{
    public AvailabilityApiTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task Get_ReturnsAvailableListingsWithPricing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

        var response = await Client.GetAsync(ApiRoute($"availability?propertyId={property.Id}&checkIn=2025-01-01&checkOut=2025-01-03&guests=1"));
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

    [Fact]
    public async Task Get_ReturnsBadRequest_WhenCheckoutIsNotAfterCheckin()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);

        var response = await Client.GetAsync(ApiRoute($"availability?propertyId={property.Id}&checkIn=2025-01-03&checkOut=2025-01-03&guests=1"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenPropertyMissing()
    {
        var response = await Client.GetAsync(ApiRoute("availability?propertyId=99999&checkIn=2025-01-01&checkOut=2025-01-03&guests=1"));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetListingAvailability_ReturnsBadRequest_WhenDatesAreInvalid()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);

        var response = await Client.GetAsync(ApiRoute($"availability/listing-availability?listingId={listing.Id}&months=2"));

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetListingAvailability_ReturnsNotFound_WhenListingMissing()
    {
        var response = await Client.GetAsync(ApiRoute("availability/listing-availability?listingId=99999&startDate=2025-01-01&months=2"));

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
