namespace Atlas.Api.Models.Reports
{
    public class DailySourceEarnings
    {
        public required string Date { get; set; } = string.Empty;
        public required string Source { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
