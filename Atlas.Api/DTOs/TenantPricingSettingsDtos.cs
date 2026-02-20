using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs;

public class TenantPricingSettingsDto
{
    public decimal ConvenienceFeePercent { get; set; }
    public decimal GlobalDiscountPercent { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public class UpdateTenantPricingSettingsDto
{
    [Range(0, 100)]
    public decimal ConvenienceFeePercent { get; set; }

    [Range(0, 100)]
    public decimal GlobalDiscountPercent { get; set; }

    [MaxLength(100)]
    public string? UpdatedBy { get; set; }
}
