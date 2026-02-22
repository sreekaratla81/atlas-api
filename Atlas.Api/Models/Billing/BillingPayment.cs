using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models.Billing;

public class BillingPayment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid InvoiceId { get; set; }

    [ValidateNever]
    public BillingInvoice Invoice { get; set; } = null!;

    [MaxLength(200)]
    public string? ProviderPaymentId { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Created";

    [Column(TypeName = "decimal(18,2)")]
    public decimal AmountInr { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
