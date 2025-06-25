
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class Listing
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Property))]
        public int PropertyId { get; set; }

        public Property Property { get; set; } = null!;
        public string Name { get; set; }
        public int Floor { get; set; }
        public string Type { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public string Status { get; set; }
        public string WifiName { get; set; }
        public string WifiPassword { get; set; }
        public int MaxGuests { get; set; }

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
