namespace Atlas.Api.DTOs
{
    public class CreateBookingRequest
    {
        public int ListingId { get; set; }
        public int GuestId { get; set; }
        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public string BookingSource { get; set; } = string.Empty;
        public decimal AmountReceived { get; set; }
        public int GuestsPlanned { get; set; }
        public int GuestsActual { get; set; }
        public decimal ExtraGuestCharge { get; set; }
        public string? PaymentStatus { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
