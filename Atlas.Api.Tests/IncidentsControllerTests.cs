using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class IncidentsControllerTests
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
        var controller = new IncidentsController(context);
        var dto = new IncidentCreateDto { ListingId = 1, Description = "d", ActionTaken = "a", Status = "open", CreatedBy = "u" };

        var result = await controller.Create(dto);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<IncidentResponseDto>(created.Value);
        Assert.Equal("d", value.Description);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Update_ReturnsNotFound_WhenMissing));
        var controller = new IncidentsController(context);
        var dto = new IncidentCreateDto { ListingId = 1, Description = "d", ActionTaken = "a", Status = "open", CreatedBy = "u" };

        var result = await controller.Update(999, dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new IncidentsController(context);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
