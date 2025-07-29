using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class ReportsControllerTests
{
    [Fact]
    public async Task GetCalendarEarnings_SpansMultipleDays()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_SpansMultipleDays))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 7, 5),
            CheckoutDate = new DateTime(2025, 7, 8),
            BookingSource = "airbnb",
            AmountReceived = 300,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Equal(3, dict.Count);
        Assert.Equal(100, dict["2025-07-05"]);
        Assert.Equal(100, dict["2025-07-06"]);
        Assert.Equal(100, dict["2025-07-07"]);
    }

    [Fact]
    public async Task GetCalendarEarnings_PartialOverlap()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_PartialOverlap))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 6, 30),
            CheckoutDate = new DateTime(2025, 7, 2),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Single(dict);
        Assert.Equal(100, dict["2025-07-01"]);
    }

    [Fact]
    public async Task GetCalendarEarnings_WithinMonth()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarEarnings_WithinMonth))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = 1,
            CheckinDate = new DateTime(2025, 7, 10),
            CheckoutDate = new DateTime(2025, 7, 12),
            BookingSource = "airbnb",
            AmountReceived = 200,
            Notes = string.Empty,
            PaymentStatus = "Paid"
        });
        await context.SaveChangesAsync();

        var controller = new ReportsController(context, NullLogger<ReportsController>.Instance);
        var result = await controller.GetCalendarEarnings(1, "2025-07");
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dict = Assert.IsType<Dictionary<string, decimal>>(ok.Value);
        Assert.Equal(2, dict.Count);
        Assert.Equal(100, dict["2025-07-10"]);
        Assert.Equal(100, dict["2025-07-11"]);
    }
}
