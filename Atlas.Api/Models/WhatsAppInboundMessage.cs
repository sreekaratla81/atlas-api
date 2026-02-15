using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class WhatsAppInboundMessage : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        public string Provider { get; set; } = string.Empty;

        [Required]
        public string ProviderMessageId { get; set; } = string.Empty;

        [Required]
        public string FromNumber { get; set; } = string.Empty;

        [Required]
        public string ToNumber { get; set; } = string.Empty;

        public DateTime ReceivedAtUtc { get; set; }

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        public string? CorrelationId { get; set; }

        public int? BookingId { get; set; }
        public Booking? Booking { get; set; }

        public int? GuestId { get; set; }
        public Guest? Guest { get; set; }
    }
}
