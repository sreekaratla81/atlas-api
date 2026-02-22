using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Tenancy;

public class TenantProvider : ITenantProvider
{
    public const string TenantSlugHeaderName = "X-Tenant-Slug";
    public const string DefaultDevSlug = "atlas";

    private readonly AppDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TenantProvider> _logger;

    public TenantProvider(AppDbContext dbContext, IWebHostEnvironment environment, ILogger<TenantProvider> logger)
    {
        _dbContext = dbContext;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>Resolves tenant from X-Tenant-Slug header. In dev/test, falls back to "atlas" for convenience.</summary>
    public async Task<Tenant?> ResolveTenantAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var slug = ResolveTenantSlugFromHeader(httpContext);

        if (string.IsNullOrWhiteSpace(slug))
        {
            if (IsDevOrTest())
            {
                _logger.LogDebug("No X-Tenant-Slug header; using dev default '{Slug}'.", DefaultDevSlug);
                slug = DefaultDevSlug;
            }
            else
            {
                return null;
            }
        }

        return await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, cancellationToken);
    }

    private static string? ResolveTenantSlugFromHeader(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(TenantSlugHeaderName, out var headerValues))
            return null;

        var headerSlug = headerValues.ToString().Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(headerSlug) ? null : headerSlug;
    }

    private bool IsDevOrTest()
    {
        return _environment.IsDevelopment()
            || _environment.IsEnvironment("IntegrationTest")
            || _environment.IsEnvironment("Testing")
            || _environment.IsEnvironment("Local");
    }
}
