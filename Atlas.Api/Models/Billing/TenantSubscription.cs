using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models.Billing;

public class TenantSubscription : IAuditable
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    public Guid PlanId { get; set; }

    [ValidateNever]
    public BillingPlan Plan { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Status { get; set; } = SubscriptionStatuses.Trial;

    public DateTime? TrialEndsAtUtc { get; set; }

    public DateTime CurrentPeriodStartUtc { get; set; }
    public DateTime CurrentPeriodEndUtc { get; set; }

    public bool AutoRenew { get; set; } = true;

    public int GracePeriodDays { get; set; } = 7;

    public DateTime? LockedAtUtc { get; set; }

    [MaxLength(30)]
    public string? LockReason { get; set; }

    public DateTime? NextInvoiceAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class SubscriptionStatuses
{
    public const string Trial = "Trial";
    public const string Active = "Active";
    public const string PastDue = "PastDue";
    public const string Suspended = "Suspended";
    public const string Canceled = "Canceled";
}

public static class LockReasons
{
    public const string CreditsExhausted = "CreditsExhausted";
    public const string InvoiceOverdue = "InvoiceOverdue";
    public const string Manual = "Manual";
    public const string ChargeFailed = "ChargeFailed";
}
