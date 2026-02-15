namespace Atlas.Api.Services.Tenancy;

public interface ITenantContextAccessor
{
    int? TenantId { get; }
}
