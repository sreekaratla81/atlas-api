using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Atlas.Api.Tests;

public class AvailabilityControllerTests
{
    [Fact]
    public async Task GetBookedDates_ReturnsEmpty_WhenNoBookings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_ReturnsEmpty_WhenNoBookings))
            .Options;

        using var context = new AppDbContext(options);
        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);

        var result = await controller.GetBookedDates(null, 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);
        Assert.Empty(response.BookedDates);
    }

    [Fact]
    public async Task GetBookedDates_ExcludesCancelledBookings()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_ExcludesCancelledBookings))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Cancelled",
                CheckinDate = today.AddDays(15),
                CheckoutDate = today.AddDays(17),
                AmountReceived = 0,
                Notes = "n"
            }
        );
        await context.SaveChangesAsync();

        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);
        var result = await controller.GetBookedDates("1", 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);

        Assert.Single(response.BookedDates);
        Assert.True(response.BookedDates.ContainsKey(1));
        Assert.Single(response.BookedDates[1]);
        Assert.Equal(today.AddDays(10), response.BookedDates[1][0].CheckinDate);
    }

    [Fact]
    public async Task GetBookedDates_FiltersByListingIds()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_FiltersByListingIds))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = 2,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(20),
                CheckoutDate = today.AddDays(22),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await context.SaveChangesAsync();

        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);
        var result = await controller.GetBookedDates("1", 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);

        Assert.Single(response.BookedDates);
        Assert.True(response.BookedDates.ContainsKey(1));
        Assert.False(response.BookedDates.ContainsKey(2));
    }

    [Fact]
    public async Task GetBookedDates_RespectsDaysAhead()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_RespectsDaysAhead))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(200),
                CheckoutDate = today.AddDays(202),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await context.SaveChangesAsync();

        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);
        var result = await controller.GetBookedDates("1", 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);

        Assert.Single(response.BookedDates);
        Assert.True(response.BookedDates.ContainsKey(1));
        Assert.Single(response.BookedDates[1]);
        Assert.Equal(today.AddDays(10), response.BookedDates[1][0].CheckinDate);
    }

    [Fact]
    public async Task GetBookedDates_UsesDateOverlapQuery()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_UsesDateOverlapQuery))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        // Booking that starts before window but ends within
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(-5),
            CheckoutDate = today.AddDays(5),
            AmountReceived = 100,
            Notes = "n"
        });
        // Booking that starts within window but ends after
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(175),
            CheckoutDate = today.AddDays(185),
            AmountReceived = 100,
            Notes = "n"
        });
        // Booking completely outside window
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            PaymentStatus = "Paid",
            CheckinDate = today.AddDays(200),
            CheckoutDate = today.AddDays(202),
            AmountReceived = 100,
            Notes = "n"
        });
        await context.SaveChangesAsync();

        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);
        var result = await controller.GetBookedDates("1", 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);

        Assert.Single(response.BookedDates);
        Assert.True(response.BookedDates.ContainsKey(1));
        Assert.Equal(2, response.BookedDates[1].Count); // Should include overlapping bookings
    }

    [Fact]
    public async Task GetBookedDates_GroupsByListingId()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetBookedDates_GroupsByListingId))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        context.Bookings.AddRange(
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(10),
                CheckoutDate = today.AddDays(12),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = 1,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(20),
                CheckoutDate = today.AddDays(22),
                AmountReceived = 100,
                Notes = "n"
            },
            new Booking
            {
                ListingId = 2,
                GuestId = guest.Id,
                BookingSource = "airbnb",
                PaymentStatus = "Paid",
                CheckinDate = today.AddDays(15),
                CheckoutDate = today.AddDays(17),
                AmountReceived = 100,
                Notes = "n"
            }
        );
        await context.SaveChangesAsync();

        var controller = new AvailabilityController(context, NullLogger<AvailabilityController>.Instance);
        var result = await controller.GetBookedDates("1,2", 180);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<BookedDatesResponse>(ok.Value);

        Assert.Equal(2, response.BookedDates.Count);
        Assert.True(response.BookedDates.ContainsKey(1));
        Assert.True(response.BookedDates.ContainsKey(2));
        Assert.Equal(2, response.BookedDates[1].Count);
        Assert.Single(response.BookedDates[2]);
    }
}


