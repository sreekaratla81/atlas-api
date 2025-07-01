using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Tests;

public class UsersControllerTests
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
        var controller = new UsersController(context);
        var user = new User { Name="n", Phone="p", Email="e", PasswordHash="ph", Role="r" };

        var result = await controller.Create(user);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<User>(created.Value);
        Assert.Equal("n", value.Name);
    }

    [Fact]
    public async Task Update_ReturnsBadRequest_WhenIdMismatch()
    {
        using var context = GetContext(nameof(Update_ReturnsBadRequest_WhenIdMismatch));
        var controller = new UsersController(context);
        var user = new User { Id=1, Name="n", Phone="p", Email="e", PasswordHash="ph", Role="r" };

        var result = await controller.Update(2, user);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new UsersController(context);

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }
}
