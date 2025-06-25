namespace Atlas.Api.Models.Reports
{
    public class CalendarBooking
    {
        public int ListingId { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
