using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingBasePrice
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        public decimal BasePrice { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";
    }
}
