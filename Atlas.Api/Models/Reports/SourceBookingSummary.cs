namespace Atlas.Api.Models.Reports
{
    public class SourceBookingSummary
    {
        public string Source { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
