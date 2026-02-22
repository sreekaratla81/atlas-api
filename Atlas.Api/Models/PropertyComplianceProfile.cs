using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

/// <summary>
/// Compliance profile for a property. Tracks ownership type, NOC requirements,
/// safety checklist, and local registration status per property.
/// </summary>
public class PropertyComplianceProfile : ITenantOwnedEntity, IAuditable
{
    [Key, ForeignKey(nameof(Property))]
    public int PropertyId { get; set; }

    [ValidateNever]
    public Property Property { get; set; } = null!;

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [MaxLength(20)]
    public string OwnershipType { get; set; } = "Owner";

    public bool OwnerNocRequired { get; set; }
    public bool OwnerNocProvided { get; set; }

    /// <summary>JSON object: { "fireExtinguisher": true, "exitSigns": false, ... }</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? SafetyChecklistJson { get; set; }

    /// <summary>JSON object: { "stateTemplateId": "KA-001", "status": "Applied", ... }</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? LocalRegistrationJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
