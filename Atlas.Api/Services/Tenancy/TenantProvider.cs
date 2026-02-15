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

    public async Task<Tenant?> ResolveTenantAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var host = httpContext.Request.Host.Host ?? "";
        var slug = ResolveTenantSlugFromHeader(httpContext)
            ?? ResolveTenantSlugFromHost(host)
            ?? ResolveTenantSlugFromDevApiHost(host)
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

    internal static string? ResolveTenantSlugFromHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return null;
        }

        var subdomain = parts[0].Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(subdomain) ? null : subdomain;
    }

    /// <summary>When the request hits the known dev API host (e.g. atlas-homes-api-dev-xxx.azurewebsites.net), resolve to default tenant so direct browser and clients without X-Tenant-Slug still work.</summary>
    private static string? ResolveTenantSlugFromDevApiHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;
        var lower = host.Trim().ToLowerInvariant();
        if (lower.Contains("atlas-homes-api-dev"))
            return DefaultTenantSlug;
        return null;
    }

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
