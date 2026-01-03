using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingDailyRate
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public DateTime Date { get; set; }

        public decimal Rate { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
