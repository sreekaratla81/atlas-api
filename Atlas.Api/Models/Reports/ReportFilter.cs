namespace Atlas.Api.Models.Reports
{
    public class ReportFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<int>? ListingIds { get; set; }
    }
}
