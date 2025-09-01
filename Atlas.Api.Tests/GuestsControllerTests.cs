using Application.Guests.Queries.SearchGuests;
using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Infrastructure.Phone;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Tests;

public class GuestsControllerTests
{
    private static (GuestsController controller, AppDbContext ctx) GetController(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var ctx = new AppDbContext(options);
        var handler = new SearchGuestsQueryHandler(ctx, new PhoneNormalizer(), new LoggerFactory().CreateLogger<SearchGuestsQueryHandler>());
        var controller = new GuestsController(ctx, handler, new PhoneNormalizer());
        return (controller, ctx);
    }

    [Fact]
    public async Task Create_SetsDefaultIdProofUrl_WhenMissing()
    {
        var (controller, _) = GetController(nameof(Create_SetsDefaultIdProofUrl_WhenMissing));
        var guest = new Guest { Name = "g", Phone = "1", Email = "e" };

        var result = await controller.Create(guest);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<Guest>(created.Value);
        Assert.Equal("N/A", value.IdProofUrl);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        var (controller, _) = GetController(nameof(Update_ReturnsBadRequest_WhenIdMismatch));
        var guest = new Guest { Id = 1, Name = "g", Phone = "1", Email = "e" };

        var result = await controller.Update(2, guest);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        var (controller, _) = GetController(nameof(Delete_ReturnsNotFound_WhenMissing));

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
