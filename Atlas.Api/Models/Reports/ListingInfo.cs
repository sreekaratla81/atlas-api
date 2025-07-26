namespace Atlas.Api.Models.Reports
{
    public class ListingInfo
    {
        public int ListingId { get; set; }
        public required string Name { get; set; } = string.Empty;
        public required string UnitCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
