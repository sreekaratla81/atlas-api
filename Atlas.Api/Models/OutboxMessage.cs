using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class OutboxMessage : ITenantOwnedEntity, IAuditable
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        /// <summary>Service Bus topic (e.g. booking.events, stay.events).</summary>
        [Required]
        [MaxLength(80)]
        public string Topic { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        public string? CorrelationId { get; set; }
        public string? EntityId { get; set; }
        public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Pending, Published, Failed</summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Pending";

        public DateTime? NextAttemptUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? PublishedAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }

        // Legacy columns (kept for compatibility; map from Topic/EntityId when reading old rows)
        [MaxLength(50)]
        public string? AggregateType { get; set; }

        [MaxLength(50)]
        public string? AggregateId { get; set; }

        public string? HeadersJson { get; set; }
    }
}
