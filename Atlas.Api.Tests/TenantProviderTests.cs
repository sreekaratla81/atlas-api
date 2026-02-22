using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Atlas.Api.Tests;

public class TenantProviderTests
{
    [Fact]
    public async Task ResolveTenantAsync_PrefersHeaderSlug()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.AddRange(
            new Tenant { Name = "Atlas", Slug = "atlas", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new Tenant { Name = "Contoso", Slug = "contoso", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Production");
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
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Development");

        var tenant = await provider.ResolveTenantAsync(new DefaultHttpContext());

        Assert.NotNull(tenant);
        Assert.Equal("atlas", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsNull_InProduction_WithoutHeader()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Production");

        var tenant = await provider.ResolveTenantAsync(new DefaultHttpContext());

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsNull_ForInactiveTenant()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Suspended", Slug = "suspended", IsActive = false, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Production");
        var context = new DefaultHttpContext();
        context.Request.Headers[TenantProvider.TenantSlugHeaderName] = "suspended";

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ResolveTenantAsync_HeaderTakesPrecedence_OverDevDefault()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.AddRange(
            new Tenant { Name = "Atlas", Slug = "atlas", IsActive = true, CreatedAtUtc = DateTime.UtcNow },
            new Tenant { Name = "Other", Slug = "other", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Development");
        var context = new DefaultHttpContext();
        context.Request.Headers[TenantProvider.TenantSlugHeaderName] = "other";

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.NotNull(tenant);
        Assert.Equal("other", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_ReturnsNull_InProduction_EvenForKnownAzureHost()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = CreateProvider(dbContext, "Production");
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("atlas-homes-api-gxdqfjc2btc0atbv.centralus-01.azurewebsites.net");

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.Null(tenant);
    }

    private static TenantProvider CreateProvider(AppDbContext dbContext, string environment)
    {
        return new TenantProvider(dbContext, new TestWebHostEnvironment(environment), NullLogger<TenantProvider>.Instance);
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
