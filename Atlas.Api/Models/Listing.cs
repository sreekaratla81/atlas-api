using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Atlas.Api.Models
{
    public class Listing : ITenantOwnedEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        [ValidateNever]
        public Tenant Tenant { get; set; } = null!;

        [ForeignKey(nameof(Property))]
        public int PropertyId { get; set; }

        [ValidateNever]
        public Property? Property { get; set; }
        public required string Name { get; set; }
        public int Floor { get; set; }
        public required string Type { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public required string Status { get; set; }
        public required string WifiName { get; set; }
        public required string WifiPassword { get; set; }
        public int MaxGuests { get; set; }

        [ValidateNever]
        public ListingPricing? Pricing { get; set; }
        [ValidateNever]
        public ICollection<ListingDailyRate> DailyRates { get; set; } = new List<ListingDailyRate>();
        [ValidateNever]
        public ICollection<ListingDailyInventory> DailyInventories { get; set; } = new List<ListingDailyInventory>();
        [ValidateNever]
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
