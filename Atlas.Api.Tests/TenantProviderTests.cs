using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;

namespace Atlas.Api.Tests;

public class TenantProviderTests
{
    [Fact]
    public async Task ResolveTenantAsync_PrefersHeaderSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.AddRange(
            new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow },
            new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));
        var context = new DefaultHttpContext();
        context.Request.Headers[TenantProvider.TenantSlugHeaderName] = "atlas";

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.NotNull(tenant);
        Assert.Equal("atlas", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_FallsBackToDefaultTenantInDevelopment()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Development"));

        var tenant = await provider.ResolveTenantAsync(new DefaultHttpContext());

        Assert.NotNull(tenant);
        Assert.Equal("atlas", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_DoesNotFallbackInProduction_WhenHostUnknown()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));

        var tenant = await provider.ResolveTenantAsync(new DefaultHttpContext());

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ResolveTenantAsync_UsesResolutionOrder_HeaderThenDefaultInDevelopmentOnly()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Development"));

        var headerContext = new DefaultHttpContext();
        headerContext.Request.Headers[TenantProvider.TenantSlugHeaderName] = "atlas";
        var headerTenant = await provider.ResolveTenantAsync(headerContext);
        Assert.NotNull(headerTenant);
        Assert.Equal("atlas", headerTenant!.Slug);

        var fallbackContext = new DefaultHttpContext();
        var fallbackTenant = await provider.ResolveTenantAsync(fallbackContext);
        Assert.NotNull(fallbackTenant);
        Assert.Equal(TenantProvider.DefaultTenantSlug, fallbackTenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_ResolvesDefaultInProduction_WhenHostIsKnownAtlasApi()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("atlas-homes-api-gxdqfjc2btc0atbv.centralus-01.azurewebsites.net");

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.NotNull(tenant);
        Assert.Equal("atlas", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsNullInProduction_WhenHostIsUnknownAndNoHeader()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("other-api.example.com");

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.Null(tenant);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}
