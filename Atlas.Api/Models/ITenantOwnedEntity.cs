namespace Atlas.Api.Models;

public interface ITenantOwnedEntity
{
    int TenantId { get; set; }
    Tenant Tenant { get; set; }
}
