using System;
using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class AutomationSchedule
    {
        public long Id { get; set; }

        public int BookingId { get; set; }

        [Required]
        [MaxLength(50)]
        public string EventType { get; set; } = string.Empty;

        public DateTime DueAtUtc { get; set; }

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = string.Empty;

        public DateTime? PublishedAtUtc { get; set; }

        public DateTime? CompletedAtUtc { get; set; }

        public int AttemptCount { get; set; }

        public string? LastError { get; set; }
    }
}
