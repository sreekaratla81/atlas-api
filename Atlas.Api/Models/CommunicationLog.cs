using System.ComponentModel.DataAnnotations;
namespace Atlas.Api.Models
{
    public class CommunicationLog : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public int? BookingId { get; set; }

        public Booking? Booking { get; set; }

        public int? GuestId { get; set; }

        public Guest? Guest { get; set; }

        [Required]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string ToAddress { get; set; } = string.Empty;

        public int? TemplateId { get; set; }

        public MessageTemplate? MessageTemplate { get; set; }

        public int TemplateVersion { get; set; }

        [Required]
        public string CorrelationId { get; set; } = string.Empty;

        [Required]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Required]
        public string Provider { get; set; } = string.Empty;

        public string? ProviderMessageId { get; set; }

        [Required]
        public string Status { get; set; } = string.Empty;

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? SentAtUtc { get; set; }
    }
}
