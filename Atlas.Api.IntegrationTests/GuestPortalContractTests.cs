using System.Net;
using System.Net.Http.Json;
using Atlas.Api.Data;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// Tests that call the API the same way the guest portal (RatebotaiRepo) does:
/// X-Tenant-Slug header, same routes and payload shapes. Validates that UI-critical flows work before deploy.
/// </summary>
[Trait("Suite", "UIContract")]
public class GuestPortalContractTests : IntegrationTestBase
{
    public GuestPortalContractTests(SqlServerTestDatabase database) : base(database) { }

    /// <summary>Guest portal: GET /listings/{id} with X-Tenant-Slug (like resolveListing). Must not return "Tenant could not be resolved".</summary>
    [Fact]
    public async Task GetListing_WithTenantHeader_Returns200()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);

        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoute($"listings/{listing.Id}"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant could not be resolved", body);
    }

    /// <summary>Guest portal: GET /listings/public with X-Tenant-Slug (fallback when direct GET by id fails).</summary>
    [Fact]
    public async Task GetListingsPublic_WithTenantHeader_Returns200()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoute("listings/public"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        var response = await Client.SendAsync(request);

        response.EnsureSuccessStatusCode();
    }

    /// <summary>Guest portal: POST /api/Razorpay/order with same payload shape as UnitBookingWidget. Validates contract; may 400 if Razorpay not configured.</summary>
    [Fact]
    public async Task RazorpayOrder_WithTenantHeader_AcceptsPayload_OrReturnsKnownError()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 120m);

        var payload = new CreateRazorpayOrderRequest
        {
            BookingDraft = new BookingDraftDto
            {
                ListingId = listing.Id,
                CheckinDate = new DateTime(2026, 3, 9),
                CheckoutDate = new DateTime(2026, 3, 10),
                Guests = 2,
                Notes = ""
            },
            Amount = 4806,
            Currency = "INR",
            GuestInfo = new GuestInfoDto
            {
                Name = "Test Guest",
                Email = "test@example.com",
                Phone = "9876543210"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiControllerRoute("Razorpay/order"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        request.Content = JsonContent.Create(payload);
        var response = await Client.SendAsync(request);

        // 200/201 = order created; 400 = validation or Razorpay config missing; 409 = duplicate order. All are "API is functioning".
            // 500 or "Tenant could not be resolved" would indicate a deploy-time failure.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Conflict,
            $"Unexpected status {response.StatusCode}. Body: {await response.Content.ReadAsStringAsync()}");

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Tenant could not be resolved", body);
    }
}
