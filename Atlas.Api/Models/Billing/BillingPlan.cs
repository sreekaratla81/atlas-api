using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models.Billing;

public class BillingPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(30)]
    public string Code { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = null!;

    public decimal MonthlyPriceInr { get; set; }

    public int CreditsIncluded { get; set; }

    public int? SeatLimit { get; set; }

    public int? ListingLimit { get; set; }

    public bool IsActive { get; set; } = true;
}
