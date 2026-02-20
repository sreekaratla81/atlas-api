using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Tenancy;

public class TenantProvider : ITenantProvider
{
    public const string TenantSlugHeaderName = "X-Tenant-Slug";
    public const string DefaultTenantSlug = "atlas";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;

    public TenantProvider(AppDbContext dbContext, IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _environment = environment;
    }

    /// <summary>Resolves tenant: 1) X-Tenant-Slug header, 2) known Atlas API host (Azure), 3) default only in non-Production.</summary>
    /// <remarks>Known-host fallback ensures /listings and Swagger work when called directly at our Azure URL without the header (single-tenant deployment). No subdomain parsing.</remarks>
    public async Task<Tenant?> ResolveTenantAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var host = httpContext.Request.Host.Host ?? "";
        var slug = ResolveTenantSlugFromHeader(httpContext)
            ?? ResolveSlugFromKnownAtlasApiHost(host)
            ?? ResolveDefaultSlug();

        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        return await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
    }

    private static string? ResolveTenantSlugFromHeader(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(TenantSlugHeaderName, out var headerValues))
        {
            return null;
        }

        var headerSlug = headerValues.ToString().Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(headerSlug) ? null : headerSlug;
    }

    /// <summary>When the request is to our Atlas API host on Azure (dev or prod), use default tenant so direct browser and Swagger work.</summary>
    private static string? ResolveSlugFromKnownAtlasApiHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var lower = host.Trim().ToLowerInvariant();
        if (lower.Contains("atlas-homes-api") && lower.Contains("azurewebsites.net"))
            return DefaultTenantSlug;
        return null;
    }

    /// <summary>Default tenant only in Development/IntegrationTest/Testing/Local.</summary>
    private string? ResolveDefaultSlug()
    {
        return _environment.IsDevelopment() ||
            _environment.IsEnvironment("IntegrationTest") ||
            _environment.IsEnvironment("Testing") ||
            _environment.IsEnvironment("Local")
            ? DefaultTenantSlug
            : null;
    }
}
