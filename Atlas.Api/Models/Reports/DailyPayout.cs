namespace Atlas.Api.Models.Reports
{
    public class DailyPayout
    {
        public DateTime Date { get; set; }
        public int ListingId { get; set; }
        public required string Listing { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public required string Status { get; set; } = string.Empty;
    }
}
