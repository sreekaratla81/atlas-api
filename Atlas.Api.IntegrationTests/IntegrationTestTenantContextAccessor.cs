using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Atlas.Api.IntegrationTests;

/// <summary>
/// For IntegrationTest environment: returns tenant from HTTP context when present (API requests),
/// otherwise 1 so that seed and scoped work (ResetAsync, DataSeeder) use tenant 1.
/// </summary>
public sealed class IntegrationTestTenantContextAccessor : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IntegrationTestTenantContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId
    {
        get
        {
            var tenant = _httpContextAccessor.HttpContext?.Items[typeof(Tenant)] as Tenant;
            return tenant?.Id ?? 1;
        }
    }
}
