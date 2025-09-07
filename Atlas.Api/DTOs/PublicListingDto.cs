using System.Collections.Generic;

namespace Atlas.Api.DTOs
{
    public class PublicListingDto
    {
        public int Id { get; set; }
        public required string Slug { get; set; }
        public required string Name { get; set; }
        public string? ShortDescription { get; set; }
        public decimal? NightlyPrice { get; set; }
        public string? CoverImageUrl { get; set; }
        public IEnumerable<string> GalleryUrls { get; set; } = new List<string>();
        public required PublicAddressDto Address { get; set; }
    }
}
