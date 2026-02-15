using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models;

public class TenantPricingSetting : ITenantOwnedEntity
{
    [Key]
    public int TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    [Column(TypeName = "decimal(5,2)")]
    public decimal ConvenienceFeePercent { get; set; } = 3.00m;

    [Column(TypeName = "decimal(5,2)")]
    public decimal GlobalDiscountPercent { get; set; } = 0.00m;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    [Column(TypeName = "varchar(100)")]
    public string? UpdatedBy { get; set; }
}
