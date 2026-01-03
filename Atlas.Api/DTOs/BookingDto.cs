namespace Atlas.Api.DTOs
{
    public class BookingDto
    {
        public int Id { get; set; }
        public int ListingId { get; set; }
        public int GuestId { get; set; }
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
        public int? BankAccountId { get; set; }
        public int GuestsPlanned { get; set; }
        public int GuestsActual { get; set; }
        public decimal ExtraGuestCharge { get; set; }
        public decimal CommissionAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public string PaymentStatus { get; set; } = "Paid";
    }
}
