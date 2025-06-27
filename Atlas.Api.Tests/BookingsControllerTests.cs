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
}
