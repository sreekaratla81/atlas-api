using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingDailyInventory : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public DateTime Date { get; set; }

        public int RoomsAvailable { get; set; }

        [Required]
        public string Source { get; set; } = "Manual";

        [MaxLength(200)]
        public string? Reason { get; set; }

        public int? UpdatedByUserId { get; set; }

        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
