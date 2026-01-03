using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Generic;
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

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());
        var request = new CreateBookingRequest
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            TotalAmount = 250,
            Currency = "USD",
            ExternalReservationId = "EXT-1",
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
        Assert.Equal("Confirmed", dto.BookingStatus);
        Assert.Equal(250, dto.TotalAmount);
        Assert.Equal("USD", dto.Currency);
        Assert.Equal("EXT-1", dto.ExternalReservationId);
    }

    [Fact]
    public async Task Create_PersistsBooking_WhenWorkflowPublisherFails()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Create_PersistsBooking_WhenWorkflowPublisherFails))
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var publisher = new Mock<IBookingWorkflowPublisher>();
        publisher
            .Setup(p => p.PublishBookingConfirmedAsync(
                It.IsAny<Booking>(),
                It.IsAny<Guest>(),
                It.IsAny<IReadOnlyCollection<CommunicationLog>>(),
                It.IsAny<OutboxMessage>()))
            .ThrowsAsync(new InvalidOperationException("Kafka down"));

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            publisher.Object);

        var request = new CreateBookingRequest
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            AmountReceived = 100,
            GuestsPlanned = 2,
            GuestsActual = 2,
            ExtraGuestCharge = 0,
            Notes = "test"
        };

        var result = await controller.Create(request);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<BookingDto>(createdResult.Value);
        Assert.Equal(listing.Id, dto.ListingId);

        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal("Booking", outbox.AggregateType);
        Assert.Equal(dto.Id.ToString(), outbox.AggregateId);
        Assert.Equal("booking-confirmed", outbox.EventType);
        Assert.Equal(1, outbox.AttemptCount);
        Assert.Contains("Kafka down", outbox.LastError);
        Assert.Null(outbox.PublishedAtUtc);
        Assert.All(context.CommunicationLogs, log => Assert.Equal("Failed", log.Status));
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

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());
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
    public async Task Create_ReturnsBadRequest_WhenOverlappingConfirmedBookingExists()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Create_ReturnsBadRequest_WhenOverlappingConfirmedBookingExists))
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        context.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = listing.Id,
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2025, 8, 3),
            BlockType = "Booking",
            Source = "System",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());
        var request = new CreateBookingRequest
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            CheckinDate = new DateTime(2025, 8, 2),
            CheckoutDate = new DateTime(2025, 8, 4),
            AmountReceived = 100,
            GuestsPlanned = 1,
            GuestsActual = 1,
            ExtraGuestCharge = 0,
            Notes = "test"
        };

        var result = await controller.Create(request);

        var badRequest = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Get_ReturnsNotFound_WhenMissing))
            .Options;
        using var context = new AppDbContext(options);
        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

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
        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());
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
        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

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
        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

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
        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

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

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

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
        var controller = new BookingsController(
            mockContext.Object,
            logger.Object,
            new NoOpBookingWorkflowPublisher());
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

    [Fact]
    public async Task Update_CancelsAvailabilityBlock_WhenBookingCancelled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Update_CancelsAvailabilityBlock_WhenBookingCancelled))
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            CheckinDate = new DateTime(2025, 9, 1),
            CheckoutDate = new DateTime(2025, 9, 3),
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var initialUpdatedAt = DateTime.UtcNow.AddDays(-1);
        context.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = listing.Id,
            BookingId = booking.Id,
            StartDate = booking.CheckinDate,
            EndDate = booking.CheckoutDate,
            BlockType = "Booking",
            Source = "System",
            Status = "Active",
            CreatedAtUtc = initialUpdatedAt,
            UpdatedAtUtc = initialUpdatedAt
        });
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());
        var request = new UpdateBookingRequest
        {
            Id = booking.Id,
            ListingId = booking.ListingId,
            GuestId = booking.GuestId,
            BookingSource = booking.BookingSource,
            BookingStatus = "Cancelled",
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            PaymentStatus = booking.PaymentStatus,
            AmountReceived = booking.AmountReceived,
            Notes = booking.Notes
        };

        var result = await controller.Update(booking.Id, request);

        Assert.IsType<NoContentResult>(result);
        var cancelledBlock = await context.AvailabilityBlocks.SingleAsync(b => b.BookingId == booking.Id);
        Assert.Equal("Cancelled", cancelledBlock.Status);
        Assert.True(cancelledBlock.UpdatedAtUtc > initialUpdatedAt);
    }

    [Fact]
    public async Task Cancel_SetsStatusAndTimestamp_AndCancelsAvailability()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Cancel_SetsStatusAndTimestamp_AndCancelsAvailability))
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

        var guest = new Guest { Name = "Guest", Phone = "1", Email = "g@example.com" };
        context.Guests.Add(guest);
        await context.SaveChangesAsync();

        var booking = new Booking
        {
            ListingId = listing.Id,
            GuestId = guest.Id,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            CheckinDate = new DateTime(2025, 9, 1),
            CheckoutDate = new DateTime(2025, 9, 3),
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        context.AvailabilityBlocks.Add(new AvailabilityBlock
        {
            ListingId = listing.Id,
            BookingId = booking.Id,
            StartDate = booking.CheckinDate,
            EndDate = booking.CheckoutDate,
            BlockType = "Booking",
            Source = "System",
            Status = "Active",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.Cancel(booking.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BookingDto>(ok.Value);
        Assert.Equal("Cancelled", dto.BookingStatus);
        Assert.NotNull(dto.CancelledAtUtc);

        var cancelledBlock = await context.AvailabilityBlocks.SingleAsync(b => b.BookingId == booking.Id);
        Assert.Equal("Cancelled", cancelledBlock.Status);
    }

    [Fact]
    public async Task Cancel_ReturnsBadRequest_WhenAlreadyCancelled()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Cancel_ReturnsBadRequest_WhenAlreadyCancelled))
            .Options;

        using var context = new AppDbContext(options);
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "airbnb",
            BookingStatus = "Cancelled",
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.Cancel(booking.Id);

        var badRequest = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CheckIn_SetsStatusAndTimestamp_WhenConfirmed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(CheckIn_SetsStatusAndTimestamp_WhenConfirmed))
            .Options;

        using var context = new AppDbContext(options);
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.CheckIn(booking.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BookingDto>(ok.Value);
        Assert.Equal("CheckedIn", dto.BookingStatus);
        Assert.NotNull(dto.CheckedInAtUtc);
    }

    [Fact]
    public async Task CheckIn_ReturnsBadRequest_WhenNotConfirmed()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(CheckIn_ReturnsBadRequest_WhenNotConfirmed))
            .Options;

        using var context = new AppDbContext(options);
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "airbnb",
            BookingStatus = "Lead",
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.CheckIn(booking.Id);

        var badRequest = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    [Fact]
    public async Task CheckOut_SetsStatusAndTimestamp_WhenCheckedIn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(CheckOut_SetsStatusAndTimestamp_WhenCheckedIn))
            .Options;

        using var context = new AppDbContext(options);
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "airbnb",
            BookingStatus = "CheckedIn",
            CheckedInAtUtc = DateTime.UtcNow.AddHours(-1),
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.CheckOut(booking.Id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<BookingDto>(ok.Value);
        Assert.Equal("CheckedOut", dto.BookingStatus);
        Assert.NotNull(dto.CheckedOutAtUtc);
    }

    [Fact]
    public async Task CheckOut_ReturnsBadRequest_WhenNotCheckedIn()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(CheckOut_ReturnsBadRequest_WhenNotCheckedIn))
            .Options;

        using var context = new AppDbContext(options);
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "airbnb",
            BookingStatus = "Confirmed",
            PaymentStatus = "Paid",
            Notes = "note"
        };
        context.Bookings.Add(booking);
        await context.SaveChangesAsync();

        var controller = new BookingsController(
            context,
            NullLogger<BookingsController>.Instance,
            new NoOpBookingWorkflowPublisher());

        var result = await controller.CheckOut(booking.Id);

        var badRequest = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, badRequest.StatusCode);
    }
}
