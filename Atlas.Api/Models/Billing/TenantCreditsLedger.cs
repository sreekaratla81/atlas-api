using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models.Billing;

/// <summary>
/// Append-only credit ledger. Derived balance = SUM(CreditsDelta).
/// Never update or delete rows; only INSERT.
/// </summary>
public class TenantCreditsLedger
{
    public long Id { get; set; }

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    [Required, MaxLength(20)]
    public string Type { get; set; } = null!;

    public int CreditsDelta { get; set; }

    [Required, MaxLength(50)]
    public string Reason { get; set; } = null!;

    [MaxLength(50)]
    public string? ReferenceType { get; set; }

    [MaxLength(50)]
    public string? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class LedgerTypes
{
    public const string Grant = "Grant";
    public const string Debit = "Debit";
    public const string Adjust = "Adjust";
    public const string Expire = "Expire";
}

public static class LedgerReasons
{
    public const string OnboardingGrant = "OnboardingGrant";
    public const string PlanGrant = "PlanGrant";
    public const string BookingCreated = "BookingCreated";
    public const string ManualAdjust = "ManualAdjust";
    public const string ExpiryJob = "ExpiryJob";
}
