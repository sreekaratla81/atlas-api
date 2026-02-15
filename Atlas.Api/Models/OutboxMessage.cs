using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class OutboxMessage : ITenantOwnedEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        public string AggregateType { get; set; } = string.Empty;

        [Required]
        public string AggregateId { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        public string? HeadersJson { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? PublishedAtUtc { get; set; }

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }
    }
}
