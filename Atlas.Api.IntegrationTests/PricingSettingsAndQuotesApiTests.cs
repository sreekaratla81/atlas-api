using System.Net;
using System.Net.Http.Json;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Api.IntegrationTests;

public class PricingSettingsAndQuotesApiTests : IntegrationTestBase
{
    public PricingSettingsAndQuotesApiTests(SqlServerTestDatabase database) : base(database) { }

    [Fact]
    public async Task PricingBreakdown_UsesTenantSettings()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 1000m, 1000m, null, "INR");

        var updateResponse = await Client.PutAsJsonAsync(ApiRoute("tenant/settings/pricing"), new UpdateTenantPricingSettingsDto
        {
            ConvenienceFeePercent = 5,
            GlobalDiscountPercent = 10,
            UpdatedBy = "integration-test"
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var breakdown = await Client.GetFromJsonAsync<PriceBreakdownDto>(ApiRoute($"pricing/guest-breakdown?listingId={listing.Id}&checkIn=2025-03-01&checkOut=2025-03-03"));
        Assert.NotNull(breakdown);
        Assert.Equal(2000m, breakdown!.BaseAmount);
        Assert.Equal(200m, breakdown.DiscountAmount);
        Assert.Equal(90m, breakdown.ConvenienceFeeAmount);
        Assert.Equal(1890m, breakdown.FinalAmount);
    }

    [Fact]
    public async Task DailySummary_ReturnsPositivePrice_WhenListingHasPricing()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 2500m, 2500m, null, "INR");

        var summary = await Client.GetFromJsonAsync<DailyPricingSummaryDto>(ApiRoute("pricing/daily-summary"));
        Assert.NotNull(summary);
        var row = summary!.Listings.FirstOrDefault(l => l.ListingId == listing.Id);
        Assert.NotNull(row);
        Assert.True(row.FinalAmount > 0, "daily-summary must return positive FinalAmount when listing has ListingPricing");
        Assert.True(row.BaseAmount > 0);
    }

    [Fact]
    public async Task QuoteValidation_FailsAcrossTenants()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Tenants.AnyAsync(t => t.Slug == "contoso"))
        {
            db.Tenants.Add(new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var property = await DataSeeder.SeedPropertyAsync(db);
        var listing = await DataSeeder.SeedListingAsync(db, property);
        await DataSeeder.SeedListingPricingAsync(db, listing, 1000m, 1000m, null, "INR");

        var issue = await Client.PostAsJsonAsync(ApiRoute("quotes"), new CreateQuoteRequestDto
        {
            ListingId = listing.Id,
            CheckIn = new DateTime(2025, 3, 10),
            CheckOut = new DateTime(2025, 3, 12),
            Guests = 2,
            QuotedBaseAmount = 1500,
            FeeMode = "CustomerPays",
            ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        });

        var payload = await issue.Content.ReadFromJsonAsync<QuoteIssueResponseDto>();
        Assert.NotNull(payload);

        var crossTenantClient = Factory.CreateClient();
        crossTenantClient.DefaultRequestHeaders.Add(TenantProvider.TenantSlugHeaderName, "contoso");
        var validate = await crossTenantClient.GetAsync(ApiRoute($"quotes/validate?token={Uri.EscapeDataString(payload!.Token)}"));

        Assert.Equal(HttpStatusCode.BadRequest, validate.StatusCode);
    }
}
