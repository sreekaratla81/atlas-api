using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs
{
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
