using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Atlas.Api.IntegrationTests;

[Trait("Suite", "Contract")]
public class AdminCalendarApiTests : IntegrationTestBase
{
    public AdminCalendarApiTests(SqlServerTestDatabase database) : base(database)
    {
    }

    [Fact]
    public async Task PutAvailability_IsIdempotent_AndGetReflectsUpdates()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var putBody = new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = new DateTime(2025, 1, 1), RoomsAvailable = 4, PriceOverride = 180m },
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = new DateTime(2025, 1, 2), RoomsAvailable = 1, PriceOverride = null }
            ]
        };

        var firstRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(putBody)
        };
        firstRequest.Headers.Add("Idempotency-Key", "calendar-key-1");

        var firstResponse = await Client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<AdminCalendarAvailabilityBulkUpsertResponseDto>();
        Assert.NotNull(firstPayload);
        Assert.False(firstPayload!.Deduplicated);

        var secondRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(putBody)
        };
        secondRequest.Headers.Add("Idempotency-Key", "calendar-key-1");

        var secondResponse = await Client.SendAsync(secondRequest);
        secondResponse.EnsureSuccessStatusCode();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<AdminCalendarAvailabilityBulkUpsertResponseDto>();
        Assert.NotNull(secondPayload);
        Assert.True(secondPayload!.Deduplicated);

        var getResponse = await Client.GetAsync(ApiRoute($"admin/calendar/availability?propertyId={property.Id}&from=2025-01-01&days=2&listingId={listing.Id}"));
        getResponse.EnsureSuccessStatusCode();
        var getPayload = await getResponse.Content.ReadFromJsonAsync<List<AdminCalendarAvailabilityCellDto>>();

        Assert.NotNull(getPayload);
        Assert.Equal(2, getPayload!.Count);
        var day1 = getPayload.Single(x => x.Date == new DateTime(2025, 1, 1));
        Assert.Equal(4, day1.RoomsAvailable);
        Assert.Equal(180m, day1.PriceOverride);
        Assert.Equal(180m, day1.EffectivePrice);

        var day2 = getPayload.Single(x => x.Date == new DateTime(2025, 1, 2));
        Assert.Equal(1, day2.RoomsAvailable);
        Assert.Null(day2.PriceOverride);
        Assert.Equal(100m, day2.EffectivePrice);
    }

    [Fact]
    public async Task PutAvailability_ReturnsBadRequest_ForInvalidValues()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);

        var response = await Client.PutAsJsonAsync(ApiRoute("admin/calendar/availability"), new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = new DateTime(2025, 1, 1), RoomsAvailable = -1, PriceOverride = -5m }
            ]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
