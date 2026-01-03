using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class AutomationSchedule
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string CronExpression { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime? LastRunAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
