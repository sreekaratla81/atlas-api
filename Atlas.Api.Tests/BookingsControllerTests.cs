using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
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
        var booking = new Booking
        {
            ListingId = 1,
            GuestId = 1,
            BookingSource = "Direct",
            PaymentStatus = "Paid",
            Notes = "test"
        };

        // Act
        var result = await controller.Create(booking);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(BookingsController.Get), createdResult.ActionName);
        Assert.Same(booking, createdResult.Value);
    }
}
