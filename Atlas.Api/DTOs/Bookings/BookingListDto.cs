namespace Atlas.Api.DTOs
{
    public sealed class BookingListDto
    {
        public int Id { get; set; }
        public string? Listing { get; set; }
        public string? Guest { get; set; }
        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public string BookingSource { get; set; } = string.Empty;
        public string BookingStatus { get; set; } = "Lead";
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "INR";
        public string? ExternalReservationId { get; set; }
        public DateTime? ConfirmationSentAtUtc { get; set; }
        public DateTime? RefundFreeUntilUtc { get; set; }
        public DateTime? CheckedInAtUtc { get; set; }
        public DateTime? CheckedOutAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public decimal AmountReceived { get; set; }
        public int GuestsPlanned { get; set; }
        public int GuestsActual { get; set; }
        public decimal ExtraGuestCharge { get; set; }
        public decimal CommissionAmount { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public string? BankAccount { get; internal set; }
        public int GuestId { get; internal set; }
        public int? BankAccountId { get; internal set; }
    }
}
