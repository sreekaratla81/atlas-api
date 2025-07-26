
using System.ComponentModel.DataAnnotations.Schema;

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

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
