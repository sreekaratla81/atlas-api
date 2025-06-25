namespace Atlas.Api.Models.Reports
{
    public class ListingInfo
    {
        public int ListingId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string UnitCode { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
