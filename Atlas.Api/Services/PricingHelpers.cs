namespace Atlas.Api.Services;

/// <summary>
/// Applies tenant-level pricing rules (e.g. global discount from TenantPricingSettings).
/// </summary>
public static class PricingHelpers
{
    /// <summary>
    /// If globalDiscountPercent > 0, returns baseRate minus discount; otherwise returns baseRate.
    /// Discount = baseRate * globalDiscountPercent / 100. Result is never negative.
    /// </summary>
    public static decimal ApplyGlobalDiscount(decimal baseRate, decimal globalDiscountPercent)
    {
        if (globalDiscountPercent <= 0)
            return baseRate;

        var discountAmount = baseRate * globalDiscountPercent / 100m;
        var afterDiscount = baseRate - discountAmount;
        return afterDiscount < 0 ? 0 : afterDiscount;
    }
}
