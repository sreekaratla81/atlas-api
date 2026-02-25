using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models;

public class ChannelConfig : ITenantOwnedEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public int PropertyId { get; set; }
    public Property Property { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string Provider { get; set; } = "channex";

    [MaxLength(500)]
    public string? ApiKey { get; set; }

    [MaxLength(200)]
    public string? ExternalPropertyId { get; set; }

    public bool IsConnected { get; set; }
    public DateTime? LastSyncAt { get; set; }

    [MaxLength(500)]
    public string? LastSyncError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
