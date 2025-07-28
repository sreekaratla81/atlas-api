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
        [ForeignKey(nameof(Property))]
        public int PropertyId { get; set; }

        [JsonIgnore]
        public Property? Property { get; set; }

        [Required]
        [ForeignKey(nameof(Guest))]
        public int GuestId { get; set; }

        public Guest Guest { get; set; }

        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public required string BookingSource { get; set; }
        public required string PaymentStatus { get; set; } = "Pending";
        public decimal AmountReceived { get; set; }
        [ForeignKey(nameof(BankAccount))]
        public int? BankAccountId { get; set; }

        [JsonIgnore]
        public virtual BankAccount? BankAccount { get; set; }
        public int? GuestsPlanned { get; set; }
        public int? GuestsActual { get; set; }
        public decimal? ExtraGuestCharge { get; set; }
        public decimal? AmountGuestPaid { get; set; }
        public decimal? CommissionAmount { get; set; }
        public required string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
