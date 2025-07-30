namespace Atlas.Api.Models.Reports
{
    public class DailySourceEarnings
    {
        public DateTime Date { get; set; }
        public required string Source { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
