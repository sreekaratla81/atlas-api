using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingPricing
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public decimal BaseRate { get; set; }

        public decimal? WeekdayRate { get; set; }

        public decimal? WeekendRate { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ListingDailyRate> DailyRates { get; set; } = new List<ListingDailyRate>();
    }
}
