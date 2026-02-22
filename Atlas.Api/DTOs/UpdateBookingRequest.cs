using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs
{
    /// <summary>Request body for updating a booking.</summary>
    public class UpdateBookingRequest
    {
        public int Id { get; set; }

        [Required]
        public int ListingId { get; set; }

        [Required]
        public int GuestId { get; set; }

        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }

        [Required]
        public string BookingSource { get; set; } = string.Empty;

        public string? BookingStatus { get; set; }
        public decimal? TotalAmount { get; set; }
        public string? Currency { get; set; }
        public string? ExternalReservationId { get; set; }
        public DateTime? ConfirmationSentAtUtc { get; set; }
        public DateTime? RefundFreeUntilUtc { get; set; }
        public DateTime? CheckedInAtUtc { get; set; }
        public DateTime? CheckedOutAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }

        [Required]
        public string PaymentStatus { get; set; } = "Paid";

        public decimal AmountReceived { get; set; }
        public int? BankAccountId { get; set; }
        public int? GuestsPlanned { get; set; }
        public int? GuestsActual { get; set; }
        public decimal? ExtraGuestCharge { get; set; }
        public decimal? CommissionAmount { get; set; }
        public string? Notes { get; set; }
    }
}
