namespace Atlas.Api.Models.Reports
{
    public class BookingInfo
    {
        public int BookingId { get; set; }
        public DateTime CheckInDate { get; set; }
        public DateTime CheckOutDate { get; set; }
        public decimal AmountReceived { get; set; }
        public int ListingId { get; set; }
        public string BookingSource { get; set; } = string.Empty;
    }
}
