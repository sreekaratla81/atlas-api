namespace Atlas.Api.Models;

public class ListingAddOn
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public Listing Listing { get; set; } = null!;
    public int AddOnServiceId { get; set; }
    public AddOnService AddOnService { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public decimal? OverridePrice { get; set; }
}
