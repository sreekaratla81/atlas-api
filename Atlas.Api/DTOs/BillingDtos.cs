using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class EntitlementsResponseDto
{
    public bool IsLocked { get; set; }
    public string? LockReason { get; set; }
    public int CreditsBalance { get; set; }
    public string SubscriptionStatus { get; set; } = null!;
    public bool IsWithinGracePeriod { get; set; }
    public string PlanCode { get; set; } = null!;
    public DateTime? PeriodEndUtc { get; set; }
    public Guid? OverdueInvoiceId { get; set; }
}

public class BillingPlanDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal MonthlyPriceInr { get; set; }
    public int CreditsIncluded { get; set; }
    public int? SeatLimit { get; set; }
    public int? ListingLimit { get; set; }
}

public class SubscribeRequestDto
{
    [Required, MaxLength(30)]
    public string PlanCode { get; set; } = null!;
    public bool AutoRenew { get; set; } = true;
}

public class InvoiceDto
{
    public Guid Id { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public decimal AmountInr { get; set; }
    public decimal TaxGstRate { get; set; }
    public decimal TaxAmountInr { get; set; }
    public decimal TotalInr { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string? PaymentLinkId { get; set; }
    public string? PdfUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class PayLinkResponseDto
{
    public string PaymentLinkUrl { get; set; } = null!;
    public string PaymentLinkId { get; set; } = null!;
}

public class CreditAdjustRequestDto
{
    public int CreditsDelta { get; set; }

    [Required, MaxLength(200)]
    public string Reason { get; set; } = null!;
}
