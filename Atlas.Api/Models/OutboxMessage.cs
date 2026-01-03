using System;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class OutboxMessage
    {
        public Guid Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string AggregateType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AggregateId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string PayloadJson { get; set; } = string.Empty;

        public string? HeadersJson { get; set; }

        public DateTime CreatedAtUtc { get; set; }

        public DateTime? PublishedAtUtc { get; set; }

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }
    }
}
