using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class ListingMedia
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        [Required]
        public string BlobName { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public int SortOrder { get; set; } = 0;
        public bool IsCover { get; set; } = false;

        public Listing Listing { get; set; } = null!;
    }
}
