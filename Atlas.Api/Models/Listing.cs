
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class Listing : ITenantOwnedEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

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

        public ListingPricing? Pricing { get; set; }
        public ICollection<ListingDailyRate> DailyRates { get; set; } = new List<ListingDailyRate>();
        public ICollection<ListingDailyInventory> DailyInventories { get; set; } = new List<ListingDailyInventory>();
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
