using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.ComponentModel.DataAnnotations;
using Moq;
using Xunit;

namespace Atlas.Api.Tests;

public class BookingsControllerTests
{
    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WhenBookingValid()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: "CreateBookingTest")
            .Options;

        using var context = new AppDbContext(options);
        // Seed required related entities
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);
        var request = new CreateBookingRequest
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            AmountReceived = 100,
            GuestsPlanned = 2,
            GuestsActual = 2,
            ExtraGuestCharge = 0,
            Notes = "test"
        };

        // Act
        var result = await controller.Create(request);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(BookingsController.Get), createdResult.ActionName);
        var dto = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(1, dto.ListingId);
        Assert.Equal(100, dto.AmountReceived);
    }

    [Fact]
    public async Task Create_AllowsNullNotes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Create_AllowsNullNotes))
            .Options;

        using var context = new AppDbContext(options);
        // Seed required related entities
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);
        var request = new CreateBookingRequest
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            AmountReceived = 100,
            GuestsPlanned = 2,
            GuestsActual = 2,
            ExtraGuestCharge = 0,
            PaymentStatus = "Pending"
            // Notes left null
        };

        var result = await controller.Create(request);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(string.Empty, dto.Notes);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Get_ReturnsNotFound_WhenMissing))
            .Options;
        using var context = new AppDbContext(options);
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);

        var result = await controller.Get(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Update_ReturnsBadRequest_WhenIdMismatch))
            .Options;
        using var context = new AppDbContext(options);
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);
        var booking = new UpdateBookingRequest { Id = 1, ListingId = 1, GuestId = 1, BookingSource = "a", Notes = "n", PaymentStatus = "Pending" };

        var result = await controller.Update(2, booking);

        Assert.IsType<BadRequestResult>(result);
    }


    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Delete_ReturnsNotFound_WhenMissing))
            .Options;
        using var context = new AppDbContext(options);
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsAllBookings_WhenNoFilters()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetAll_ReturnsAllBookings_WhenNoFilters))
            .Options;
        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();
        context.Bookings.AddRange(
            new Booking { ListingId = 1, GuestId = guest.Id, BookingSource = "a", Notes = "n", PaymentStatus = "Pending", CheckinDate = new DateTime(2025, 7, 10), CheckoutDate = new DateTime(2025, 7, 12) },
            new Booking { ListingId = 1, GuestId = guest.Id, BookingSource = "a", Notes = "n", PaymentStatus = "Pending", CheckinDate = new DateTime(2025, 8, 1), CheckoutDate = new DateTime(2025, 8, 3) }
        );
        await context.SaveChangesAsync();
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);

        var result = await controller.GetAll(null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<BookingListDto>>(ok.Value);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task GetAll_FiltersBookings_WhenCheckinRangeProvided()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetAll_FiltersBookings_WhenCheckinRangeProvided))
            .Options;
        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();
        context.Bookings.AddRange(
            new Booking { ListingId = 1, GuestId = guest.Id, BookingSource = "a", Notes = "n", PaymentStatus = "Pending", CheckinDate = new DateTime(2025, 7, 10), CheckoutDate = new DateTime(2025, 7, 12) },
            new Booking { ListingId = 1, GuestId = guest.Id, BookingSource = "a", Notes = "n", PaymentStatus = "Pending", CheckinDate = new DateTime(2025, 8, 1), CheckoutDate = new DateTime(2025, 8, 3) }
        );
        await context.SaveChangesAsync();
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);

        var result = await controller.GetAll(new DateTime(2025, 7, 1), new DateTime(2025, 7, 31), null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<BookingListDto>>(ok.Value);
        Assert.Single(items);
        Assert.Equal(new DateTime(2025, 7, 10), items[0].CheckinDate);
    }

    [Fact]
    public async Task GetAll_ProjectsGuestName()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(GetAll_ProjectsGuestName))
            .Options;

        using var context = new AppDbContext(options);
        var guest = new Guest { Name = "Tester", Phone = "1", Email = "t@example.com" };
        context.Guests.Add(guest);
        context.Bookings.Add(new Booking
        {
            ListingId = 1,
            GuestId = guest.Id,
            BookingSource = "a",
            Notes = "n",
            PaymentStatus = "Pending",
            CheckinDate = new DateTime(2025, 7, 10),
            CheckoutDate = new DateTime(2025, 7, 12)
        });
        await context.SaveChangesAsync();

        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);

        var result = await controller.GetAll(null, null, null);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var items = Assert.IsType<List<BookingListDto>>(ok.Value);
        Assert.Single(items);
        Assert.Equal("Tester 1", items[0].Guest);
    }

    [Fact]
    public async Task Update_ReturnsConcurrencyError_WhenSaveFails()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Update_ReturnsConcurrencyError_WhenSaveFails))
            .Options;

        // Seed related entities and a booking using a real context
        using (var seed = new AppDbContext(options))
        {
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
            seed.Properties.Add(property);
            await seed.SaveChangesAsync();

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
            seed.Listings.Add(listing);

            var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
            seed.Guests.Add(guest);
            await seed.SaveChangesAsync();

            seed.Bookings.Add(new Booking
            {
                Id = 1,
                ListingId = listing.Id,
                GuestId = guest.Id,
                BookingSource = "a",
                Notes = "n",
                PaymentStatus = "Pending"
            });
            await seed.SaveChangesAsync();
        }

        var mockContext = new Moq.Mock<AppDbContext>(options) { CallBase = true };
        mockContext.Setup(x => x.SaveChangesAsync(Moq.It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new DbUpdateConcurrencyException());

        var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<BookingsController>>();
        var controller = new BookingsController(mockContext.Object, logger.Object);
        var booking = new UpdateBookingRequest { Id = 1, ListingId = 1, GuestId = 1, BookingSource = "a", Notes = "n", PaymentStatus = "Pending" };

        var result = await controller.Update(1, booking);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, status.StatusCode);
        logger.Verify(l => l.Log(
            Microsoft.Extensions.Logging.LogLevel.Error,
            Moq.It.IsAny<Microsoft.Extensions.Logging.EventId>(),
            Moq.It.IsAny<Moq.It.IsAnyType>(),
            Moq.It.IsAny<DbUpdateConcurrencyException?>(),
            (Func<Moq.It.IsAnyType, Exception?, string>)Moq.It.IsAny<object>()),
            Moq.Times.Once);
    }

}
