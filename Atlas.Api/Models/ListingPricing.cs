using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingPricing : ITenantOwnedEntity
    {
        [Required]
        [Key]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        public Listing Listing { get; set; } = null!;

        public decimal BaseNightlyRate { get; set; }

        public decimal? WeekendNightlyRate { get; set; }

        public decimal? ExtraGuestRate { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

        public ICollection<ListingDailyRate> DailyRates { get; set; } = new List<ListingDailyRate>();
    }
}
