using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Atlas.Api.Models
{
    public class Booking : ITenantOwnedEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

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

        [MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string? BookingSource { get; set; }
        [Required]
        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string BookingStatus { get; set; } = "Lead";
        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalAmount { get; set; }
        [Required]
        [MaxLength(10)]
        [Column(TypeName = "varchar(10)")]
        public string Currency { get; set; } = "INR";
        [MaxLength(100)]
        [Column(TypeName = "varchar(100)")]
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
        
        [JsonIgnore]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public int? GuestsPlanned { get; set; }
        public int? GuestsActual { get; set; }
        public decimal? ExtraGuestCharge { get; set; }
        public decimal? CommissionAmount { get; set; }
        [Required]
        public string Notes { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
