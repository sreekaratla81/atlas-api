using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

/// <summary>
/// Extended legal/tax/compliance profile for a tenant (1:1 with Tenant).
/// Separated from Tenant to keep the core model lean and the onboarding data isolated.
/// </summary>
public class TenantProfile : IAuditable
{
    [Key, ForeignKey(nameof(Tenant))]
    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [MaxLength(200)]
    public string? LegalName { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(30)]
    public string BusinessType { get; set; } = "Individual";

    [MaxLength(500)]
    public string? RegisteredAddressLine { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(50)]
    public string? State { get; set; }

    [MaxLength(10)]
    public string? Pincode { get; set; }

    /// <summary>Last 4 digits of PAN for display; full PAN stored hashed in PanHash.</summary>
    [MaxLength(4)]
    public string? PanLast4 { get; set; }

    /// <summary>BCrypt hash of full PAN for verification without storing plaintext.</summary>
    [MaxLength(200)]
    public string? PanHash { get; set; }

    [MaxLength(15)]
    public string? Gstin { get; set; }

    [MaxLength(50)]
    public string? PlaceOfSupplyState { get; set; }

    [MaxLength(200)]
    public string? PrimaryEmail { get; set; }

    [MaxLength(20)]
    public string? PrimaryPhone { get; set; }

    [MaxLength(30)]
    public string OnboardingStatus { get; set; } = "Draft";

    public int? UpdatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
