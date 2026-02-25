using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models;

public class BookingInvoice : ITenantOwnedEntity
{
    public int Id { get; set; }
    public int TenantId { get; set; }

    [ValidateNever]
    public Tenant Tenant { get; set; } = null!;

    public int BookingId { get; set; }

    [ValidateNever]
    public Booking Booking { get; set; } = null!;

    [Required, MaxLength(50)]
    public string InvoiceNumber { get; set; } = "";

    [MaxLength(200)] public string? GuestName { get; set; }
    [MaxLength(200)] public string? GuestEmail { get; set; }
    [MaxLength(20)] public string? GuestPhone { get; set; }
    [MaxLength(200)] public string? PropertyName { get; set; }
    [MaxLength(200)] public string? ListingName { get; set; }

    public DateTime CheckinDate { get; set; }
    public DateTime CheckoutDate { get; set; }
    public int Nights { get; set; }

    public decimal BaseAmount { get; set; }
    public decimal GstRate { get; set; }
    public decimal GstAmount { get; set; }
    public decimal TotalAmount { get; set; }

    [MaxLength(15)] public string? SupplierGstin { get; set; }
    [MaxLength(200)] public string? SupplierLegalName { get; set; }
    [MaxLength(500)] public string? SupplierAddress { get; set; }
    [MaxLength(50)] public string? PlaceOfSupply { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Status { get; set; } = "generated";
}
