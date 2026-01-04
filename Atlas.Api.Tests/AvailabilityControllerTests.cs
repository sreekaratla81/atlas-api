using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class AvailabilityControllerTests
{
    [Fact]
    public async Task GetAvailability_ReturnsBadRequest_WhenDatesInvalid()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetAvailability_ReturnsBadRequest_WhenDatesInvalid))
            .Options;

        using var context = new AppDbContext(options);
        var service = new AvailabilityService(context);
        var controller = new AvailabilityController(context, service, NullLogger<AvailabilityController>.Instance);

        var result = await controller.GetAvailability(1, new DateTime(2025, 1, 2), new DateTime(2025, 1, 2), 1);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_ReturnsOk_WithListings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetAvailability_ReturnsOk_WithListings))
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
            Currency = "USD"
        });
        await context.SaveChangesAsync();

        var service = new AvailabilityService(context);
        var controller = new AvailabilityController(context, service, NullLogger<AvailabilityController>.Instance);

        var result = await controller.GetAvailability(property.Id, new DateTime(2025, 1, 1), new DateTime(2025, 1, 3), 1);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }
}
