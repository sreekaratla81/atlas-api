using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class PaymentsControllerTests
{
    private static AppDbContext GetContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        using var context = GetContext(nameof(Create_ReturnsCreatedAtAction));
        context.Bookings.Add(new Booking { Id = 1, ListingId = 1, GuestId = 1, BookingSource = "direct", PaymentStatus = "Pending", CheckinDate = DateTime.UtcNow, CheckoutDate = DateTime.UtcNow.AddDays(1), GuestsPlanned = 1, GuestsActual = 1, ExtraGuestCharge = 0, CommissionAmount = 0 });
        await context.SaveChangesAsync();

        var controller = new PaymentsController(context);
        var item = new PaymentCreateDto { BookingId = 1, Amount = 10, Method = "cash", Type = "type", ReceivedOn = DateTime.UtcNow, Note = "n" };

        var result = await controller.Create(item);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<PaymentResponseDto>(created.Value);
        Assert.Equal(1, value.BookingId);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Update_ReturnsNotFound_WhenMissing));
        var controller = new PaymentsController(context);
        var item = new PaymentUpdateDto { BookingId = 1, Amount = 1, Method="cash", Type="type", ReceivedOn=DateTime.UtcNow, Note="n" };

        var result = await controller.Update(2, item);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new PaymentsController(context);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
