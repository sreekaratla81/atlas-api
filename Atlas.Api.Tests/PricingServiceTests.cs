using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Atlas.Api.Tests;

public class PricingServiceTests
{
    [Fact]
    public async Task GetPricingAsync_UsesDailyOverridesOverWeekendRate()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetPricingAsync_UsesDailyOverridesOverWeekendRate))
            .Options;

        using var context = new AppDbContext(options);
        var property = new Property
        {
            Name = "Property",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "000",
            CommissionPercent = 10,
            Status = "Active"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        context.ListingPricings.Add(new ListingPricing
        {
            ListingId = listing.Id,
            Listing = listing,
            BaseNightlyRate = 110m,
            WeekendNightlyRate = 150m,
            Currency = "USD"
        });
        context.ListingDailyRates.Add(new ListingDailyRate
        {
            ListingId = listing.Id,
            Listing = listing,
            Date = new DateTime(2025, 1, 4),
            NightlyRate = 200m,
            Currency = "USD",
            Source = "Manual"
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context, new StubTenantPricingSettingsService());

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 1, 3), new DateTime(2025, 1, 5));

        Assert.Equal(2, result.NightlyRates.Count);
        Assert.Equal(150m, result.NightlyRates[0].Rate);
        Assert.Equal(200m, result.NightlyRates[1].Rate);
        Assert.Equal(350m, result.TotalPrice);
    }

    [Fact]
    public async Task GetPricingAsync_CalculatesTotalUsingWeekdayAndWeekendRates()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetPricingAsync_CalculatesTotalUsingWeekdayAndWeekendRates))
            .Options;

        using var context = new AppDbContext(options);
        var property = new Property
        {
            Name = "Property",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "000",
            CommissionPercent = 10,
            Status = "Active"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        context.ListingPricings.Add(new ListingPricing
        {
            ListingId = listing.Id,
            Listing = listing,
            BaseNightlyRate = 100m,
            WeekendNightlyRate = 120m,
            Currency = "USD"
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context, new StubTenantPricingSettingsService());

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 1, 2), new DateTime(2025, 1, 5));

        Assert.Equal(3, result.NightlyRates.Count);
        Assert.Equal(340m, result.TotalPrice);
        Assert.Equal(new[] { 100m, 120m, 120m }, result.NightlyRates.Select(rate => rate.Rate));
    }

    [Fact]
    public async Task GetPricingAsync_AppliesEffectivePricePrecedence_DailyOverride_ThenWeekend_ThenBase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetPricingAsync_AppliesEffectivePricePrecedence_DailyOverride_ThenWeekend_ThenBase))
            .Options;

        using var context = new AppDbContext(options);
        var property = new Property
        {
            Name = "Property",
            Address = "Addr",
            Type = "House",
            OwnerName = "Owner",
            ContactPhone = "000",
            CommissionPercent = 10,
            Status = "Active"
        };
        context.Properties.Add(property);
        await context.SaveChangesAsync();

        var listing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        context.Listings.Add(listing);
        await context.SaveChangesAsync();

        context.ListingPricings.Add(new ListingPricing
        {
            ListingId = listing.Id,
            Listing = listing,
            BaseNightlyRate = 100m,
            WeekendNightlyRate = 150m,
            Currency = "USD"
        });

        context.ListingDailyRates.Add(new ListingDailyRate
        {
            ListingId = listing.Id,
            Listing = listing,
            Date = new DateTime(2025, 1, 5),
            NightlyRate = 200m,
            Currency = "USD",
            Source = "Manual"
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context, new StubTenantPricingSettingsService());

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 1, 3), new DateTime(2025, 1, 6));

        Assert.Equal(new[] { 150m, 150m, 200m }, result.NightlyRates.Select(r => r.Rate));
        Assert.Equal(500m, result.TotalPrice);
    }
}


internal sealed class StubTenantPricingSettingsService : ITenantPricingSettingsService
{
    public Task<TenantPricingSetting> GetCurrentAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new TenantPricingSetting { ConvenienceFeePercent = 3m, GlobalDiscountPercent = 0m });

    public void Invalidate(int tenantId) { }

    public Task<TenantPricingSetting> UpdateCurrentAsync(UpdateTenantPricingSettingsDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(new TenantPricingSetting { ConvenienceFeePercent = request.ConvenienceFeePercent, GlobalDiscountPercent = request.GlobalDiscountPercent });
}
