using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Atlas.Api.Tests;

public class TenantResolutionMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsBadRequestInProductionWhenTenantIsMissing()
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider
            .Setup(provider => provider.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Tenant?)null);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, tenantProvider.Object, new TestWebHostEnvironment("Production"));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextWhenTenantResolved()
    {
        var tenant = new Tenant { Id = 9, Name = "Atlas", Slug = "atlas", Status = "Active", CreatedAtUtc = DateTime.UtcNow };
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider
            .Setup(provider => provider.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenant);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, tenantProvider.Object, new TestWebHostEnvironment("Production"));

        Assert.True(nextCalled);
        Assert.Equal(tenant, context.Items[typeof(Tenant)]);
    }

    [Fact]
    public async Task InvokeAsync_SkipsTenantResolution_ForHealthAndSwagger()
    {
        var tenantProvider = new Mock<ITenantProvider>();

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, NullLogger<TenantResolutionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context, tenantProvider.Object, new TestWebHostEnvironment("Production"));

        Assert.True(nextCalled);
        tenantProvider.Verify(p => p.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()), Times.Never);
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
