using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
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

    private static ITenantContextAccessor StubAccessor(int tenantId = 1)
        => new StubTenantContextAccessor(tenantId);

    [Fact]
    public async Task Create_ReturnsCreatedAtAction()
    {
        using var context = GetContext(nameof(Create_ReturnsCreatedAtAction));
        var controller = new UsersController(context, StubAccessor());
        var dto = new UserCreateDto { Name = "n", Email = "e@e.com", Phone = "p", Role = "r", Password = "secret1" };

        var result = await controller.Create(dto);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var value = Assert.IsType<UserResponseDto>(created.Value);
        Assert.Equal("n", value.Name);
    }

    [Fact]
    public async Task Create_HashesPassword()
    {
        using var context = GetContext(nameof(Create_HashesPassword));
        var controller = new UsersController(context, StubAccessor());
        var dto = new UserCreateDto { Name = "n", Email = "e@e.com", Phone = "p", Role = "r", Password = "secret1" };

        await controller.Create(dto);

        var user = await context.Users.IgnoreQueryFilters().FirstAsync();
        Assert.NotEqual("secret1", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("secret1", user.PasswordHash));
    }

    [Fact]
    public async Task Update_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Update_ReturnsNotFound_WhenMissing));
        var controller = new UsersController(context, StubAccessor());
        var dto = new UserCreateDto { Name = "n", Email = "e@e.com", Phone = "p", Role = "r", Password = "secret1" };

        var result = await controller.Update(2, dto);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new UsersController(context, StubAccessor());

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class StubTenantContextAccessor(int tenantId) : ITenantContextAccessor
    {
        public int? TenantId => tenantId;
    }
}
