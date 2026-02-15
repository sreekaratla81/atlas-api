
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models
{
    public class Property : ITenantOwnedEntity
    {
        public int Id { get; set; }
        public int TenantId { get; set; }
        [ValidateNever]
        public Tenant Tenant { get; set; } = null!;
        public required string Name { get; set; }
        public required string Address { get; set; }
        public required string Type { get; set; }
        public required string OwnerName { get; set; }
        public required string ContactPhone { get; set; }
        public decimal? CommissionPercent { get; set; }
        public required string Status { get; set; }
    }
}
