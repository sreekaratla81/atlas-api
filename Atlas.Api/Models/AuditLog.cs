using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

/// <summary>
/// Immutable audit trail for tenant-scoped actions. Sensitive fields in PayloadJson
/// must be redacted before storage (e.g. PAN → "XXXX1234").
/// </summary>
public class AuditLog : ITenantOwnedEntity
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    public int? ActorUserId { get; set; }

    [Required, MaxLength(100)]
    public string Action { get; set; } = null!;

    [MaxLength(50)]
    public string? EntityType { get; set; }

    [MaxLength(50)]
    public string? EntityId { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(max)")]
    public string? PayloadJson { get; set; }
}
