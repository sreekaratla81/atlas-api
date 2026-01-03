using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingDailyRate
    {
        public long Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public DateTime Date { get; set; }

        public decimal NightlyRate { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        [Required]
        public string Source { get; set; } = "Manual";

        [MaxLength(200)]
        public string? Reason { get; set; }

        public int? UpdatedByUserId { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
