using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class OutboxMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();

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
