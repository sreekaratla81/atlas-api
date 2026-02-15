using System.Net;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Services.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// Tests that call the API the same way the admin portal (atlas-admin-portal) does:
/// X-Tenant-Slug header, same routes. Validates that UI-critical flows work before deploy.
/// </summary>
[Trait("Suite", "UIContract")]
public class AdminPortalContractTests : IntegrationTestBase
{
    public AdminPortalContractTests(SqlServerTestDatabase database) : base(database) { }

    /// <summary>Admin portal: GET admin/calendar/availability with X-Tenant-Slug (like AvailabilityCalendar).</summary>
    [Fact]
    public async Task GetCalendarAvailability_WithTenantHeader_Returns200Or404()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);

        var url = ApiRoute($"admin/calendar/availability?propertyId={property.Id}&from=2025-01-01&days=1&listingId={listing.Id}");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        var response = await Client.SendAsync(request);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound,
            $"Unexpected status {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant could not be resolved", body);
    }

    /// <summary>Admin portal: GET admin/reports/bookings with X-Tenant-Slug.</summary>
    [Fact]
    public async Task GetReportsBookings_WithTenantHeader_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoute("admin/reports/bookings"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant could not be resolved", body);
    }

    /// <summary>Admin portal: PUT admin/calendar/availability (bulk upsert) with X-Tenant-Slug and Idempotency-Key.</summary>
    [Fact]
    public async Task PutCalendarAvailability_WithTenantHeader_AcceptsPayload()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var payload = new AdminCalendarAvailabilityBulkUpsertRequestDto
        {
            Cells =
            [
                new AdminCalendarAvailabilityCellUpsertDto
                {
                    ListingId = listing.Id,
                    Date = new DateTime(2025, 6, 1),
                    RoomsAvailable = 2,
                    PriceOverride = 150m
                }
            ]
        };

        using var request = new HttpRequestMessage(HttpMethod.Put, ApiRoute("admin/calendar/availability"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(payload);
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant could not be resolved", body);
    }
}
