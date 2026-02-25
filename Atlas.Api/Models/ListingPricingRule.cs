using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models;

public class ListingPricingRule : ITenantOwnedEntity
{
    public int Id { get; set; }

    public int TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;

    [Required][MaxLength(20)] public string RuleType { get; set; } = "LOS";

    // LOS conditions
    public int? MinNights { get; set; }
    public int? MaxNights { get; set; }

    // Seasonal conditions
    public DateTime? SeasonStart { get; set; }
    public DateTime? SeasonEnd { get; set; }
    [MaxLength(100)] public string? Label { get; set; }

    public decimal DiscountPercent { get; set; }
    public bool IsActive { get; set; } = true;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
