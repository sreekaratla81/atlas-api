namespace Atlas.Api.Models.Reports
{
    public class SourceBookingSummary
    {
        public required string Source { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
