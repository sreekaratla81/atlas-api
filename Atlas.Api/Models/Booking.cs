using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Atlas.Api.Models
{
    public class Booking
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;


        [Required]
        public int GuestId { get; set; }

        [ForeignKey("GuestId")]
        public Guest Guest { get; set; } = null!;

        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }

        [Required]
        public string BookingSource { get; set; } = string.Empty;
        [Required]
        public string BookingStatus { get; set; } = "Lead";
        public decimal TotalAmount { get; set; }
        [Required]
        public string Currency { get; set; } = "INR";
        public string? ExternalReservationId { get; set; }
        public DateTime? ConfirmationSentAtUtc { get; set; }
        public DateTime? RefundFreeUntilUtc { get; set; }
        public DateTime? CheckedInAtUtc { get; set; }
        public DateTime? CheckedOutAtUtc { get; set; }
        public DateTime? CancelledAtUtc { get; set; }

        [Required]
        public string PaymentStatus { get; set; } = "Paid";
        public decimal AmountReceived { get; set; }
        [ForeignKey(nameof(BankAccount))]
        public int? BankAccountId { get; set; }

        [JsonIgnore]
        public virtual BankAccount? BankAccount { get; set; }
        public int? GuestsPlanned { get; set; }
        public int? GuestsActual { get; set; }
        public decimal? ExtraGuestCharge { get; set; }
        public decimal? CommissionAmount { get; set; }
        [Required]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
