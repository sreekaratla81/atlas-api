using Atlas.Api.Controllers;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class PropertiesControllerTests
{
    private static ITenantContextAccessor StubAccessor(int? tenantId = 1) => new StubTenantContextAccessor(tenantId);

    private static AppDbContext GetContext(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetAll_ReturnsProperties()
    {
        using var context = GetContext(nameof(GetAll_ReturnsProperties));
        context.Properties.Add(new Property { Id = 1, Name="n", Address="a", Type="t", OwnerName="o", ContactPhone="c", Status="s" });
        await context.SaveChangesAsync();
        var controller = new PropertiesController(context, NullLogger<PropertiesController>.Instance, StubAccessor());

        var result = await controller.GetAll();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var props = Assert.IsAssignableFrom<IEnumerable<PropertyResponseDto>>(ok.Value);
        Assert.Single(props);
    }

    [Fact]
    public async Task Delete_ReturnsNotFound_WhenMissing()
    {
        using var context = GetContext(nameof(Delete_ReturnsNotFound_WhenMissing));
        var controller = new PropertiesController(context, NullLogger<PropertiesController>.Instance, StubAccessor());

        var result = await controller.Delete(1);

        Assert.IsType<NotFoundResult>(result);
    }

    private sealed class StubTenantContextAccessor(int? tenantId) : ITenantContextAccessor
    {
        public int? TenantId => tenantId;
    }
}
