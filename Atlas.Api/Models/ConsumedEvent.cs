using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class ConsumedEvent : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        public string ConsumerName { get; set; } = string.Empty;

        [Required]
        public string EventId { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        public DateTime ProcessedAtUtc { get; set; }

        public string? PayloadHash { get; set; }

        public string? Status { get; set; }
    }
}
