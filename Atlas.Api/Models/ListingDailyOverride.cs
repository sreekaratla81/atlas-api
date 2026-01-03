using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingDailyOverride
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public DateTime Date { get; set; }

        public decimal Price { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
