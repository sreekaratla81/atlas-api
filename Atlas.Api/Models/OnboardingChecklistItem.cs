using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

/// <summary>
/// Tracks onboarding tasks per tenant. Stage determines when the item blocks progress:
/// FastStart = needed to create draft listing, PublishGate = blocks publish, PostPublish = compliance deadline.
/// </summary>
public class OnboardingChecklistItem : ITenantOwnedEntity, IAuditable
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(50)]
    public string Key { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;

    [MaxLength(20)]
    public string Stage { get; set; } = "FastStart";

    [MaxLength(20)]
    public string Status { get; set; } = "Pending";

    public bool Blocking { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public int? EvidenceDocId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
