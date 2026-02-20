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

    /// <summary>Resolves tenant from X-Tenant-Slug header only. No host or subdomain-based resolution.</summary>
    /// <remarks>In Production, requests without the header get 400 (see TenantResolutionMiddleware). Default tenant is used only in Development/IntegrationTest so the issue is caught in dev before prod.</remarks>
    public async Task<Tenant?> ResolveTenantAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var slug = ResolveTenantSlugFromHeader(httpContext)
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

    /// <summary>Default tenant only in non-Production so missing header fails in prod and is caught by tests in dev.</summary>
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
