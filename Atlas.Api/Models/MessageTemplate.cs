using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class MessageTemplate
    {
        public int Id { get; set; }

        [Required]
        public string TemplateKey { get; set; } = string.Empty;

        [Required]
        public string Channel { get; set; } = string.Empty;

        public string? Subject { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
