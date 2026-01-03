using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class CommunicationLog
    {
        public int Id { get; set; }

        [Required]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public string Recipient { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = "Pending";

        public string? ProviderMessageId { get; set; }

        public string? ErrorMessage { get; set; }

        [ForeignKey(nameof(Booking))]
        public int BookingId { get; set; }

        public Booking Booking { get; set; } = null!;

        [ForeignKey(nameof(MessageTemplate))]
        public int? MessageTemplateId { get; set; }

        public MessageTemplate? MessageTemplate { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
