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
    public async Task ResolveTenantAsync_PrefersHeaderSlugOverHost()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.AddRange(
            new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow },
            new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));
        var context = new DefaultHttpContext();
        context.Request.Headers[TenantProvider.TenantSlugHeaderName] = "atlas";
        context.Request.Host = new HostString("contoso.atlashomestays.com");

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.NotNull(tenant);
        Assert.Equal("atlas", tenant!.Slug);
    }

    [Fact]
    public async Task ResolveTenantAsync_UsesHostSubdomainWhenHeaderMissing()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("contoso.atlashomestays.com");

        var tenant = await provider.ResolveTenantAsync(context);

        Assert.NotNull(tenant);
        Assert.Equal("contoso", tenant!.Slug);
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
    public async Task ResolveTenantAsync_DoesNotFallbackInProduction()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.Add(new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Production"));

        var tenant = await provider.ResolveTenantAsync(new DefaultHttpContext());

        Assert.Null(tenant);
    }

    [Fact]
    public async Task ResolveTenantAsync_UsesResolutionOrder_HeaderThenSubdomainThenDefaultInDevelopment()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Tenants.AddRange(
            new Tenant { Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow },
            new Tenant { Name = "Contoso", Slug = "contoso", Status = "Active", CreatedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync();

        var provider = new TenantProvider(dbContext, new TestWebHostEnvironment("Development"));

        var headerContext = new DefaultHttpContext();
        headerContext.Request.Headers[TenantProvider.TenantSlugHeaderName] = "atlas";
        headerContext.Request.Host = new HostString("contoso.atlashomestays.com");

        var headerTenant = await provider.ResolveTenantAsync(headerContext);
        Assert.NotNull(headerTenant);
        Assert.Equal("atlas", headerTenant!.Slug);

        var subdomainContext = new DefaultHttpContext();
        subdomainContext.Request.Host = new HostString("contoso.atlashomestays.com");

        var subdomainTenant = await provider.ResolveTenantAsync(subdomainContext);
        Assert.NotNull(subdomainTenant);
        Assert.Equal("contoso", subdomainTenant!.Slug);

        var fallbackTenant = await provider.ResolveTenantAsync(new DefaultHttpContext());
        Assert.NotNull(fallbackTenant);
        Assert.Equal(TenantProvider.DefaultTenantSlug, fallbackTenant!.Slug);
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
