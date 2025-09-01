using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class GuestsControllerTests
{
    private static AppDbContext GetContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Create_SetsDefaultIdProofUrl_WhenMissing()
    {
        using var context = GetContext(nameof(Create_SetsDefaultIdProofUrl_WhenMissing));
        var controller = new GuestsController(context);
        var guest = new Guest { Name = "g", Phone = "1", Email = "e" };

        var result = await controller.Create(guest);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<Guest>(created.Value);
        Assert.Equal("N/A", value.IdProofUrl);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        using var context = GetContext(nameof(Update_ReturnsBadRequest_WhenIdMismatch));
        var controller = new GuestsController(context);
        var guest = new Guest { Id = 1, Name = "g", Phone = "1", Email = "e" };

        var result = await controller.Update(2, guest);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new GuestsController(context);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
