using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models.Billing;

public class BillingInvoice : IAuditable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountInr { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxGstRate { get; set; } = 18m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmountInr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalInr { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = InvoiceStatuses.Draft;

    public DateTime? DueAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }

    [MaxLength(20)]
    public string Provider { get; set; } = "Manual";

    [MaxLength(200)]
    public string? ProviderInvoiceId { get; set; }

    [MaxLength(500)]
    public string? PaymentLinkId { get; set; }

    [MaxLength(1000)]
    public string? PdfUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class InvoiceStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";
    public const string Paid = "Paid";
    public const string Void = "Void";
    public const string Overdue = "Overdue";
}
