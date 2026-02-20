using Atlas.Api.Models;

namespace Atlas.Api.Services.Tenancy;

public interface ITenantProvider
{
    Task<Tenant?> ResolveTenantAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
