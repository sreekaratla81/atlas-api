using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [ForeignKey(nameof(Guest))]
        public int GuestId { get; set; }

        public Guest Guest { get; set; } = null!;

        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public TimeSpan? PlannedCheckinTime { get; set; }
        public TimeSpan? ActualCheckinTime { get; set; }
        public TimeSpan? PlannedCheckoutTime { get; set; }
        public TimeSpan? ActualCheckoutTime { get; set; }
        public string BookingSource { get; set; }
        public string PaymentStatus { get; set; }
        public decimal AmountReceived { get; set; }
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaymentDate { get; set; }
    }
}
