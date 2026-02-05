using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Models.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

public class AdminReportsControllerTests
{
    [Fact]
    public async Task GetMonthlyEarnings_ComputesNetAndFees()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "MonthlyEarningsTest")
            .Options;

        using var context = new AppDbContext(options);
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1).AddMonths(-11);

        context.Bookings.AddRange(
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddDays(1), BookingSource = "airbnb", AmountReceived = 100, Notes = string.Empty, PaymentStatus="Pending" },
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddMonths(1).AddDays(2), BookingSource = "booking.com", AmountReceived = 200, Notes = string.Empty, PaymentStatus="Pending" },
            new Booking { ListingId = 1, GuestId = 1, CheckinDate = start.AddMonths(1).AddDays(3), BookingSource = "agoda", AmountReceived = 300, Notes = string.Empty, PaymentStatus="Pending" }
        );
        await context.SaveChangesAsync();

        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetMonthlyEarnings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var summaries = Assert.IsAssignableFrom<IEnumerable<MonthlyEarningsSummary>>(ok.Value);
        var list = summaries.ToList();

        var key0 = start.ToString("yyyy-MM");
        var key1 = start.AddMonths(1).ToString("yyyy-MM");

        var first = list.Single(x => x.Month == key0);
        Assert.Equal(100, first.TotalNet);
        Assert.Equal(100 * 0.16m, first.TotalFees);

        var second = list.Single(x => x.Month == key1);
        var net = 200 + 300;
        var fees = 200 * 0.15m + 300 * 0.18m;
        Assert.Equal(net, second.TotalNet);
        Assert.Equal(fees, second.TotalFees);
    }

    [Fact]
    public async Task GetBookings_FiltersByListingId()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookings_FiltersByListingId))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking { Id=1, ListingId=1, GuestId=1, CheckinDate=DateTime.UtcNow, CheckoutDate=DateTime.UtcNow, BookingSource="a", AmountReceived=1, Notes="n", PaymentStatus="Pending" },
            new Booking { Id=2, ListingId=2, GuestId=1, CheckinDate=DateTime.UtcNow, CheckoutDate=DateTime.UtcNow, BookingSource="a", AmountReceived=1, Notes="n", PaymentStatus="Pending" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetBookings(null, null, new List<int>{1});
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BookingInfo>>(ok.Value);
        Assert.Single(list);
        Assert.Equal(1, list.First().ListingId);
    }

    [Fact]
    public async Task GetBookingSourceReport_ReturnsCounts()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookingSourceReport_ReturnsCounts))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking { ListingId=1, GuestId=1, CheckinDate=DateTime.UtcNow, CheckoutDate=DateTime.UtcNow, BookingSource="A", AmountReceived=10, Notes="", PaymentStatus="Pending" },
            new Booking { ListingId=1, GuestId=1, CheckinDate=DateTime.UtcNow, CheckoutDate=DateTime.UtcNow, BookingSource="B", AmountReceived=20, Notes="", PaymentStatus="Pending" },
            new Booking { ListingId=1, GuestId=1, CheckinDate=DateTime.UtcNow, CheckoutDate=DateTime.UtcNow, BookingSource="A", AmountReceived=30, Notes="", PaymentStatus="Pending" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetBookingSourceReport(null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<SourceBookingSummary>>(ok.Value).ToList();
        Assert.Equal(2, list.Count);
        var a = list.Single(x => x.Source == "A");
        Assert.Equal(2, a.Count);
    }

    [Fact]
    public async Task GetBookings_ReturnsAll_WhenNoFilters()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetBookings_ReturnsAll_WhenNoFilters))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking { Id = 1, ListingId = 1, GuestId = 1, CheckinDate = DateTime.UtcNow.AddDays(-1), CheckoutDate = DateTime.UtcNow, BookingSource = "a", AmountReceived = 1, Notes = "n", PaymentStatus = "Pending" },
            new Booking { Id = 2, ListingId = 2, GuestId = 1, CheckinDate = DateTime.UtcNow.AddDays(-2), CheckoutDate = DateTime.UtcNow, BookingSource = "b", AmountReceived = 1, Notes = "n", PaymentStatus = "Pending" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetBookings(null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<BookingInfo>>(ok.Value);
        Assert.Equal(2, list.Count());
    }

    [Fact]
    public async Task GetListings_ReturnsAll_WithStatusFlag()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetListings_ReturnsAll_WithStatusFlag))
            .Options;
        using var context = new AppDbContext(options);
        var property = new Property { Id = 1, Name = "p", Address = "a", Type = "t", OwnerName = "o", ContactPhone = "c", Status = "s" };
        context.Properties.Add(property);
        context.Listings.AddRange(
            new Listing { Id = 1, Property = property, PropertyId = 1, Name = "L1", Floor = 1, Type = "T", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 },
            new Listing { Id = 2, Property = property, PropertyId = 1, Name = "L2", Floor = 1, Type = "T", Status = "Inactive", WifiName = "w", WifiPassword = "p", MaxGuests = 2 }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetListings();
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<ListingInfo>>(ok.Value).ToList();
        Assert.Equal(2, list.Count);
        Assert.True(list.Single(l => l.ListingId == 1).IsActive);
        Assert.False(list.Single(l => l.ListingId == 2).IsActive);
    }

    [Fact]
    public async Task GetPayouts_ReturnsGroupedTotals()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetPayouts_ReturnsGroupedTotals))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.Add(new Booking { Id = 1, ListingId = 1, GuestId = 1, CheckinDate = DateTime.UtcNow, CheckoutDate = DateTime.UtcNow, BookingSource = "a", AmountReceived = 1, Notes = "n", PaymentStatus = "Pending" });
        context.Payments.AddRange(
            new Payment { BookingId = 1, Amount = 50, Method = "cash", Type = "pay", ReceivedOn = DateTime.UtcNow, Note = "n" },
            new Payment { BookingId = 1, Amount = 75, Method = "cash", Type = "pay", ReceivedOn = DateTime.UtcNow, Note = "n" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetPayouts(null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<DailyPayout>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal(125, list[0].Amount);
    }

    [Fact]
    public async Task GetDailyPayoutReport_FiltersByListing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetDailyPayoutReport_FiltersByListing))
            .Options;
        using var context = new AppDbContext(options);
        var property = new Property { Id = 1, Name = "p", Address = "a", Type = "t", OwnerName = "o", ContactPhone = "c", Status = "s" };
        context.Properties.Add(property);
        var listing1 = new Listing { Id = 1, Property = property, PropertyId = 1, Name = "L1", Floor = 1, Type = "T", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 };
        var listing2 = new Listing { Id = 2, Property = property, PropertyId = 1, Name = "L2", Floor = 1, Type = "T", Status = "Active", WifiName = "w", WifiPassword = "p", MaxGuests = 2 };
        context.Listings.AddRange(listing1, listing2);
        context.Bookings.AddRange(
            new Booking { Id = 1, ListingId = 1, Listing = listing1, GuestId = 1, CheckinDate = DateTime.UtcNow, CheckoutDate = DateTime.UtcNow, BookingSource = "a", AmountReceived = 1, Notes = "n", PaymentStatus = "Pending" },
            new Booking { Id = 2, ListingId = 2, Listing = listing2, GuestId = 1, CheckinDate = DateTime.UtcNow, CheckoutDate = DateTime.UtcNow, BookingSource = "a", AmountReceived = 2, Notes = "n", PaymentStatus = "Pending" }
        );
        context.Payments.AddRange(
            new Payment { BookingId = 1, Amount = 50, Method = "cash", Type = "pay", ReceivedOn = DateTime.UtcNow.Date, Note = "n" },
            new Payment { BookingId = 2, Amount = 70, Method = "cash", Type = "pay", ReceivedOn = DateTime.UtcNow.Date, Note = "n" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetDailyPayoutReport(null, null, new List<int> { 1 });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<DailyPayout>>(ok.Value).ToList();
        Assert.Single(list);
        Assert.Equal(50, list[0].Amount);
        Assert.Equal("L1", list[0].Listing);
    }

    [Fact]
    public async Task GetCalendarBookings_ReturnsDataForListing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(nameof(GetCalendarBookings_ReturnsDataForListing))
            .Options;
        using var context = new AppDbContext(options);
        context.Bookings.AddRange(
            new Booking { Id = 1, ListingId = 1, GuestId = 1, CheckinDate = DateTime.UtcNow.Date, CheckoutDate = DateTime.UtcNow.Date.AddDays(1), BookingSource = "a", AmountReceived = 10, Notes = "n", PaymentStatus = "Pending" },
            new Booking { Id = 2, ListingId = 1, GuestId = 1, CheckinDate = DateTime.UtcNow.Date.AddDays(1), CheckoutDate = DateTime.UtcNow.Date.AddDays(2), BookingSource = "a", AmountReceived = 0, Notes = "n", PaymentStatus = "Pending" }
        );
        await context.SaveChangesAsync();
        var controller = new AdminReportsController(context, NullLogger<AdminReportsController>.Instance);

        var result = await controller.GetCalendarBookings(null, null, new List<int> { 1 });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var list = Assert.IsAssignableFrom<IEnumerable<CalendarBooking>>(ok.Value).ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal("Paid", list[0].Status);
        Assert.Equal("Unpaid", list[1].Status);
    }
}
