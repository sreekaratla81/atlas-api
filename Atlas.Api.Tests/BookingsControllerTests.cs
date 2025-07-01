using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
        var controller = new BookingsController(context, NullLogger<BookingsController>.Instance);
        var request = new CreateBookingRequest
        {
            ListingId = 1,
            GuestId = 1,
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
        var booking = new Booking { Id = 1, ListingId = 1, GuestId = 1, BookingSource="a", Notes="n", PaymentStatus="Pending" };

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
    public async Task Update_ReturnsConcurrencyError_WhenSaveFails()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: nameof(Update_ReturnsConcurrencyError_WhenSaveFails))
            .Options;
        using var context = new ThrowingDbContext(options);
        context.Bookings.Add(new Booking { Id = 1, ListingId = 1, GuestId = 1, BookingSource="a", Notes="n", PaymentStatus="Pending" });
        await context.SaveChangesAsync();
        context.ThrowOnSave = true;
        var logger = new Moq.Mock<Microsoft.Extensions.Logging.ILogger<BookingsController>>();
        var controller = new BookingsController(context, logger.Object);
        var booking = new Booking { Id = 1, ListingId = 1, GuestId = 1, BookingSource="a", Notes="n", PaymentStatus="Pending" };

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

    private class ThrowingDbContext : AppDbContext
    {
        public bool ThrowOnSave { get; set; }
        public ThrowingDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
                throw new DbUpdateConcurrencyException();
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
