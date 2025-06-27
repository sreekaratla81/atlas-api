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

        [JsonIgnore]
        public Listing? Listing { get; set; }

        [Required]
        [ForeignKey(nameof(Guest))]
        public int GuestId { get; set; }

        [JsonIgnore]
        public Guest? Guest { get; set; }

        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public string BookingSource { get; set; }
        public string PaymentStatus { get; set; }
        public decimal AmountReceived { get; set; }
        public string Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
