
namespace Atlas.Api.Models
{
    public class Listing
    {
        public int Id { get; set; }
        public int PropertyId { get; set; }
        public string Name { get; set; }
        public int Floor { get; set; }
        public string Type { get; set; }
        public string? CheckInTime { get; set; }
        public string? CheckOutTime { get; set; }
        public string Status { get; set; }
        public string WifiName { get; set; }
        public string WifiPassword { get; set; }
        public int MaxGuests { get; set; }
    }
}
