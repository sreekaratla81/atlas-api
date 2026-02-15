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
        var slug = ResolveTenantSlugFromHeader(httpContext)
            ?? ResolveTenantSlugFromHost(httpContext.Request.Host.Host)
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
