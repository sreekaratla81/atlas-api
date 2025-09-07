
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace Atlas.Api.Models
{
    public class Listing
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Property))]
        public int PropertyId { get; set; }

        public required Property Property { get; set; }
        public required string Name { get; set; }
        public int Floor { get; set; }
        public required string Type { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public required string Status { get; set; }
        public required string WifiName { get; set; }
        public required string WifiPassword { get; set; }
        public int MaxGuests { get; set; }

        public string Slug { get; set; } = string.Empty;
        public bool IsPublic { get; set; } = true;
        public string BlobContainer { get; set; } = "listing-images";
        public string BlobPrefix { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
        public decimal? NightlyPrice { get; set; }
        public string? ShortDescription { get; set; }

        public ICollection<ListingMedia> Media { get; set; } = new List<ListingMedia>();

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
