using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class AvailabilityServiceTests
{
    [Fact]
    public async Task GetAvailabilityAsync_UsesOverridesAndTotals()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetAvailabilityAsync_UsesOverridesAndTotals))
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

        context.ListingBasePrices.Add(new ListingBasePrice
        {
            ListingId = listing.Id,
            Listing = listing,
            BasePrice = 100m,
            Currency = "USD"
        });
        context.ListingDailyOverrides.Add(new ListingDailyOverride
        {
            ListingId = listing.Id,
            Listing = listing,
            Date = new DateTime(2025, 1, 2),
            Price = 150m
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);

        var response = await service.GetAvailabilityAsync(property.Id, new DateTime(2025, 1, 1), new DateTime(2025, 1, 3), 1);

        Assert.Single(response.Listings);
        var availability = response.Listings.Single();
        Assert.Equal(2, availability.NightlyRates.Count);
        Assert.Equal(250m, availability.TotalPrice);
        Assert.Equal(100m, availability.NightlyRates[0].Price);
        Assert.Equal(150m, availability.NightlyRates[1].Price);
    }

    [Fact]
    public async Task GetAvailabilityAsync_ExcludesListingsWithOverlaps()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetAvailabilityAsync_ExcludesListingsWithOverlaps))
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
        var availableListing = new Listing
        {
            PropertyId = property.Id,
            Property = property,
            Name = "Listing 2",
            Floor = 1,
            Type = "Room",
            Status = "Available",
            WifiName = "wifi",
            WifiPassword = "pass",
            MaxGuests = 2
        };
        context.Listings.AddRange(listing, availableListing);
        await context.SaveChangesAsync();

        context.ListingBasePrices.AddRange(
            new ListingBasePrice
            {
                ListingId = listing.Id,
                Listing = listing,
                BasePrice = 100m,
                Currency = "USD"
            },
            new ListingBasePrice
            {
                ListingId = availableListing.Id,
                Listing = availableListing,
                BasePrice = 120m,
                Currency = "USD"
            });

        context.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = listing.Id,
            StartDate = new DateTime(2025, 2, 1),
            EndDate = new DateTime(2025, 2, 5),
            BlockType = "Booking",
            Source = "System",
            Status = "Active"
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);

        var response = await service.GetAvailabilityAsync(property.Id, new DateTime(2025, 2, 2), new DateTime(2025, 2, 4), 1);

        Assert.Single(response.Listings);
        Assert.Equal(availableListing.Id, response.Listings.Single().ListingId);
    }
}
