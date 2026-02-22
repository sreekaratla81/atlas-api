using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

/// <summary>
/// KYC/compliance document uploaded by a host. Multiple per tenant.
/// DocType examples: PAN, Aadhaar, Passport, LeaseAgreement, OwnerNOC, TourismReg, FireNOC.
/// </summary>
public class HostKycDocument : ITenantOwnedEntity, IAuditable
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(50)]
    public string DocType { get; set; } = null!;

    [MaxLength(1000)]
    public string? FileUrl { get; set; }

    [MaxLength(200)]
    public string? OriginalFileName { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }
    public int? VerifiedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
