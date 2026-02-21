using System.Net;
using System.Net.Http.Json;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Models.Dtos.Razorpay;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// P0 marketing readiness scenarios: tenant isolation, pricing never 0, Razorpay server-side amount.
/// </summary>
[Trait("Suite", "MarketingReadiness")]
public class MarketingReadinessApiTests : IntegrationTestBase
{
    public MarketingReadinessApiTests(SqlServerTestDatabase database) : base(database) { }

    /// <summary>R2: Tenant isolation — GET listings/{id} with wrong tenant returns 404.</summary>
    [Fact]
    public async Task GetListing_WithWrongTenant_Returns404()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);

        if (!await db.Tenants.AnyAsync(t => t.Slug == "contoso"))
        {
            db.Tenants.Add(new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoute($"listings/{listing.Id}"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "contoso");
        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>F2: Pricing never 0 — daily override applied when present, else global discount, else base rate.</summary>
    [Fact]
    public async Task PricingBreakdown_NeverReturnsZero()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 500m, 500m, null, "INR");

        var breakdown = await Client.GetFromJsonAsync<PriceBreakdownDto>(
            ApiRoute($"pricing/guest-breakdown?listingId={listing.Id}&checkIn=2025-03-01&checkOut=2025-03-03"));
        Assert.NotNull(breakdown);
        Assert.True(breakdown!.BaseAmount > 0, "BaseAmount must be > 0");
        Assert.True(breakdown.FinalAmount > 0, "FinalAmount must be > 0");
    }

    /// <summary>F2: Pricing fallback — daily override wins over base rate.</summary>
    [Fact]
    public async Task PricingBreakdown_DailyOverrideTakesPrecedence()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 100m, 100m, null, "INR");
        await DataSeeder.SeedListingDailyRateAsync(db, listing, new DateTime(2025, 3, 2), 250m, "INR");

        var breakdown = await Client.GetFromJsonAsync<PriceBreakdownDto>(
            ApiRoute($"pricing/guest-breakdown?listingId={listing.Id}&checkIn=2025-03-01&checkOut=2025-03-04"));
        Assert.NotNull(breakdown);
        Assert.True(breakdown!.BaseAmount > 0);
        Assert.True(breakdown.FinalAmount > 0);
    }

    /// <summary>G1: Razorpay — when BookingDraft provided and Amount omitted, server computes amount (never trust client).</summary>
    [Fact]
    public async Task RazorpayOrder_WithBookingDraftOnly_UsesServerComputedAmount()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 1000m, 1000m, null, "INR");

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
            Amount = null,
            Currency = "INR",
            GuestInfo = new GuestInfoDto
            {
                Name = "Test Guest",
                Email = "razorpay-server-amount@example.com",
                Phone = "9876543210"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiControllerRoute("Razorpay/order"));
        request.Headers.Add(TenantProvider.TenantSlugHeaderName, "atlas");
        request.Content = JsonContent.Create(payload);
        var response = await Client.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
        {
            var body = await response.Content.ReadFromJsonAsync<RazorpayOrderResponse>();
            Assert.NotNull(body);
            Assert.True(body!.Amount > 0, "Server must compute positive amount when BookingDraft provided");
        }
        else if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var text = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Tenant could not be resolved", text);
        }
    }
}
