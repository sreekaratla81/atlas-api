using Atlas.Api.Models;

namespace Atlas.Api.Services.Tenancy;

public class HttpTenantContextAccessor : ITenantContextAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpTenantContextAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? TenantId => (_httpContextAccessor.HttpContext?.Items[typeof(Tenant)] as Tenant)?.Id;
}
