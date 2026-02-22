using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
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
    public async Task GetAvailability_ReturnsTenantScopedRows()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureTenantAsync(db, "contoso", "Contoso");

        var atlasProperty = await DataSeeder.SeedPropertyAsync(db);
        var atlasListing = await DataSeeder.SeedListingAsync(db, atlasProperty);
        await DataSeeder.SeedListingPricingAsync(db, atlasListing, 100m, 120m);
        db.ListingDailyInventories.Add(new ListingDailyInventory
        {
            ListingId = atlasListing.Id,
            Date = new DateTime(2025, 1, 1),
            RoomsAvailable = 3,
            Source = "Manual"
        });
        await db.SaveChangesAsync();

        var contosoProperty = new Property
        {
            TenantId = 2,
            Name = "Contoso Property",
            Address = "Addr 2",
            Type = "Villa",
            OwnerName = "Owner 2",
            ContactPhone = "222",
            CommissionPercent = 10,
            Status = "Active"
        };
        db.Properties.Add(contosoProperty);
        await db.SaveChangesAsync();

        var contosoListing = new Listing
        {
            TenantId = 2,
            PropertyId = contosoProperty.Id,
            Property = contosoProperty,
            Name = "Contoso Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "w2",
            WifiPassword = "p2",
            MaxGuests = 2
        };
        db.Listings.Add(contosoListing);
        await db.SaveChangesAsync();

        db.ListingPricings.Add(new ListingPricing
        {
            TenantId = 2,
            ListingId = contosoListing.Id,
            Listing = contosoListing,
            BaseNightlyRate = 150m,
            WeekendNightlyRate = 170m,
            Currency = "INR"
        });
        db.ListingDailyInventories.Add(new ListingDailyInventory
        {
            TenantId = 2,
            ListingId = contosoListing.Id,
            Listing = contosoListing,
            Date = new DateTime(2025, 1, 1),
            RoomsAvailable = 5,
            Source = "Manual"
        });
        await db.SaveChangesAsync();

        var atlasRequest = new HttpRequestMessage(HttpMethod.Get, ApiRoute($"admin/calendar/availability?propertyId={atlasProperty.Id}&from=2025-01-01&days=1&listingId={atlasListing.Id}"));
        atlasRequest.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        var atlasResponse = await Client.SendAsync(atlasRequest);
        atlasResponse.EnsureSuccessStatusCode();

        var atlasPayload = await atlasResponse.Content.ReadFromJsonAsync<List<AdminCalendarAvailabilityCellDto>>();
        Assert.NotNull(atlasPayload);
        Assert.Single(atlasPayload!);
        Assert.Equal(atlasListing.Id, atlasPayload[0].ListingId);

        var crossTenantRequest = new HttpRequestMessage(HttpMethod.Get, ApiRoute($"admin/calendar/availability?propertyId={atlasProperty.Id}&from=2025-01-01&days=1&listingId={atlasListing.Id}"));
        crossTenantRequest.Headers.Add(TenantProvider.TenantSlugHeaderName, "contoso");
        var crossTenantResponse = await Client.SendAsync(crossTenantRequest);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantResponse.StatusCode);
    }

    [Fact]
    public async Task PutAvailability_WritesAndUpdates_ListingDailyRate_And_ListingDailyInventory()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var firstPut = await Client.PutAsJsonAsync(ApiRoute("admin/calendar/availability"), new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = new DateTime(2025, 1, 1), RoomsAvailable = 4, PriceOverride = 180m }
            ]
        });
        firstPut.EnsureSuccessStatusCode();

        var createdInventory = await db.ListingDailyInventories.AsNoTracking().SingleAsync(i => i.ListingId == listing.Id && i.Date == new DateTime(2025, 1, 1));
        var createdRate = await db.ListingDailyRates.AsNoTracking().SingleAsync(r => r.ListingId == listing.Id && r.Date == new DateTime(2025, 1, 1));
        Assert.Equal(4, createdInventory.RoomsAvailable);
        Assert.Equal(180m, createdRate.NightlyRate);

        var secondPut = await Client.PutAsJsonAsync(ApiRoute("admin/calendar/availability"), new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = new DateTime(2025, 1, 1), RoomsAvailable = 2, PriceOverride = null }
            ]
        });
        secondPut.EnsureSuccessStatusCode();

        var updatedInventory = await db.ListingDailyInventories.AsNoTracking().SingleAsync(i => i.ListingId == listing.Id && i.Date == new DateTime(2025, 1, 1));
        var remainingRates = await db.ListingDailyRates.AsNoTracking().Where(r => r.ListingId == listing.Id && r.Date == new DateTime(2025, 1, 1)).ToListAsync();

        Assert.Equal(2, updatedInventory.RoomsAvailable);
        Assert.Empty(remainingRates);
    }

    [Fact]
    public async Task PutAvailability_UpsertingPriceOverrideTwice_UpdatesSingleRateRow()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var date = new DateTime(2025, 1, 2);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(new AdminCalendarAvailabilityBulkUpsertRequestDto
            {
                Cells =
                [
                    new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = date, RoomsAvailable = 3, PriceOverride = 210m }
                ]
            })
        };
        firstRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var firstResponse = await Client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(new AdminCalendarAvailabilityBulkUpsertRequestDto
            {
                Cells =
                [
                    new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = date, RoomsAvailable = 3, PriceOverride = 260m }
                ]
            })
        };
        secondRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var secondResponse = await Client.SendAsync(secondRequest);
        secondResponse.EnsureSuccessStatusCode();

        var rates = await db.ListingDailyRates
            .AsNoTracking()
            .Where(r => r.ListingId == listing.Id && r.Date == date)
            .ToListAsync();

        Assert.Single(rates);
        Assert.Equal(260m, rates[0].NightlyRate);

        var getResponse = await Client.GetAsync(ApiRoute($"admin/calendar/availability?propertyId={property.Id}&from={date:yyyy-MM-dd}&days=1&listingId={listing.Id}"));
        getResponse.EnsureSuccessStatusCode();

        var cells = await getResponse.Content.ReadFromJsonAsync<List<AdminCalendarAvailabilityCellDto>>();
        var cell = Assert.Single(cells!);
        Assert.Equal(260m, cell.PriceOverride);
    }

    [Fact]
    public async Task PutAvailability_UpsertingInventoryTwice_UpdatesSingleInventoryRow()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var date = new DateTime(2025, 1, 3);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(new AdminCalendarAvailabilityBulkUpsertRequestDto
            {
                Cells =
                [
                    new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = date, RoomsAvailable = 5, PriceOverride = 180m }
                ]
            })
        };
        firstRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var firstResponse = await Client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(new AdminCalendarAvailabilityBulkUpsertRequestDto
            {
                Cells =
                [
                    new AdminCalendarAvailabilityCellUpsertDto { ListingId = listing.Id, Date = date, RoomsAvailable = 1, PriceOverride = 180m }
                ]
            })
        };
        secondRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var secondResponse = await Client.SendAsync(secondRequest);
        secondResponse.EnsureSuccessStatusCode();

        var inventoryRows = await db.ListingDailyInventories
            .AsNoTracking()
            .Where(i => i.ListingId == listing.Id && i.Date == date)
            .ToListAsync();

        Assert.Single(inventoryRows);
        Assert.Equal(1, inventoryRows[0].RoomsAvailable);

        var getResponse = await Client.GetAsync(ApiRoute($"admin/calendar/availability?propertyId={property.Id}&from={date:yyyy-MM-dd}&days=1&listingId={listing.Id}"));
        getResponse.EnsureSuccessStatusCode();

        var cells = await getResponse.Content.ReadFromJsonAsync<List<AdminCalendarAvailabilityCellDto>>();
        var cell = Assert.Single(cells!);
        Assert.Equal(1, cell.RoomsAvailable);
    }

    [Fact]
    public async Task PutAvailability_ReturnsNotFound_ForCrossTenantListingAccess()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await EnsureTenantAsync(db, "contoso", "Contoso");

        var atlasProperty = await DataSeeder.SeedPropertyAsync(db);
        var atlasListing = await DataSeeder.SeedListingAsync(db, atlasProperty);

        var request = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"))
        {
            Content = JsonContent.Create(new AdminCalendarAvailabilityBulkUpsertRequestDto
            {
                Cells =
                [
                    new AdminCalendarAvailabilityCellUpsertDto
                    {
                        ListingId = atlasListing.Id,
                        Date = new DateTime(2025, 1, 1),
                        RoomsAvailable = 2,
                        PriceOverride = 200m
                    }
                ]
            })
        };
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "contoso");

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task EnsureTenantAsync(AppDbContext db, string slug, string name)
    {
        if (await db.Tenants.AnyAsync(t => t.Slug == slug))
        {
            return;
        }

        db.Tenants.Add(new Tenant
        {
            Name = name,
            Slug = slug,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }
}
