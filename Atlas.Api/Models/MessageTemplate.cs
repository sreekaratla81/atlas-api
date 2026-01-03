using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models
{
    public class MessageTemplate
    {
        public int Id { get; set; }

        [Required]
        public string TemplateKey { get; set; } = string.Empty;

        [Required]
        public string EventType { get; set; } = string.Empty;

        [Required]
        public string Channel { get; set; } = string.Empty;

        [Required]
        public string ScopeType { get; set; } = string.Empty;

        public int? ScopeId { get; set; }

        [Required]
        public string Language { get; set; } = string.Empty;

        public int TemplateVersion { get; set; } = 1;

        public bool IsActive { get; set; } = true;

        public string? Subject { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
