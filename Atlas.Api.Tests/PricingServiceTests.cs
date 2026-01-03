using Atlas.Api.Data;
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
            BaseRate = 100m,
            WeekdayRate = 110m,
            WeekendRate = 150m,
            Currency = "USD"
        });
        context.ListingDailyRates.Add(new ListingDailyRate
        {
            ListingId = listing.Id,
            Listing = listing,
            Date = new DateTime(2025, 1, 4),
            Rate = 200m
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 1, 3), new DateTime(2025, 1, 5));

        Assert.Equal(2, result.NightlyRates.Count);
        Assert.Equal(110m, result.NightlyRates[0].Rate);
        Assert.Equal(200m, result.NightlyRates[1].Rate);
        Assert.Equal(310m, result.TotalPrice);
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
            BaseRate = 90m,
            WeekdayRate = 100m,
            WeekendRate = 120m,
            Currency = "USD"
        });
        await context.SaveChangesAsync();

        var service = new PricingService(context);

        var result = await service.GetPricingAsync(listing.Id, new DateTime(2025, 1, 2), new DateTime(2025, 1, 5));

        Assert.Equal(3, result.NightlyRates.Count);
        Assert.Equal(320m, result.TotalPrice);
        Assert.Equal(new[] { 100m, 100m, 120m }, result.NightlyRates.Select(rate => rate.Rate));
    }
}
