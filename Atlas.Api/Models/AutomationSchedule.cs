using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class AutomationSchedule : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        public int BookingId { get; set; }

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public DateTime DueAtUtc { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        public DateTime? PublishedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }
    }
}
