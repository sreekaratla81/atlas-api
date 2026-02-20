using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class MessageTemplateCreateUpdateDto
{
    public string? TemplateKey { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Channel { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string ScopeType { get; set; } = string.Empty;

    public int? ScopeId { get; set; }

    [Required]
    [MaxLength(10)]
    public string Language { get; set; } = string.Empty;

    public int TemplateVersion { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    [MaxLength(200)]
    public string? Subject { get; set; }

    [Required]
    public string Body { get; set; } = string.Empty;
}

public class MessageTemplateResponseDto
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public string? TemplateKey { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string ScopeType { get; set; } = string.Empty;
    public int? ScopeId { get; set; }
    public string Language { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public bool IsActive { get; set; }
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
