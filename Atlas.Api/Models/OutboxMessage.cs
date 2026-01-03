using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class OutboxMessage
    {
        public int Id { get; set; }

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string Payload { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime? ProcessedAtUtc { get; set; }

        public string Status { get; set; } = "Pending";

        public string? ErrorMessage { get; set; }
    }
}
