using Atlas.Api.Controllers;
using Atlas.Api.Data;
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
        var controller = new PaymentsController(context);
        var item = new Payment { BookingId = 1, Amount = 10, Method = "cash", Type = "type", ReceivedOn = DateTime.UtcNow, Note="n" };

        var result = await controller.Create(item);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<Payment>(created.Value);
        Assert.Equal(1, value.BookingId);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        using var context = GetContext(nameof(Update_ReturnsBadRequest_WhenIdMismatch));
        var controller = new PaymentsController(context);
        var item = new Payment { Id = 1, BookingId = 1, Amount = 1, Method="cash", Type="type", ReceivedOn=DateTime.UtcNow, Note="n" };

        var result = await controller.Update(2, item);

        Assert.IsType<BadRequestResult>(result);
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
