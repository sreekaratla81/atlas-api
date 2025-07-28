using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class ListingsControllerTests
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
        // Seed the property so the controller can attach it
        context.Properties.Add(new Property { Id = 1, Name = "p", Address = "a", Type = "t", OwnerName = "o", ContactPhone = "c", CommissionPercent = 0, Status = "s" });
        await context.SaveChangesAsync();

        var controller = new ListingsController(context, NullLogger<ListingsController>.Instance);
        var listing = new Listing { Id = 1, PropertyId = 1, Property = new Property { Id = 1, Name = "p", Address = "a", Type = "t", OwnerName = "o", ContactPhone = "c", CommissionPercent = 0, Status = "s" }, Name = "L", Floor = 1, Type = "t", Status = "Active", WifiName="w", WifiPassword="pass", MaxGuests=2 };

        var result = await controller.Create(listing);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<Listing>(created.Value);
        Assert.Equal("L", value.Name);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        using var context = GetContext(nameof(Update_ReturnsBadRequest_WhenIdMismatch));
        var controller = new ListingsController(context, NullLogger<ListingsController>.Instance);
        var listing = new Listing { Id = 1, PropertyId = 1, Property = new Property { Id=1, Name="p", Address="a", Type="t", OwnerName="o", ContactPhone="c", Status="s" }, Name = "L", Floor = 1, Type = "t", Status = "Active", WifiName="w", WifiPassword="pass", MaxGuests=2 };

        var result = await controller.Update(2, listing);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Get_ReturnsNotFound_WhenMissing));
        var controller = new ListingsController(context, NullLogger<ListingsController>.Instance);

        var result = await controller.Get(1);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new ListingsController(context, NullLogger<ListingsController>.Instance);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
