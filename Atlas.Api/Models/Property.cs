
namespace Atlas.Api.Models
{
    public class Property
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Address { get; set; }
        public required string Type { get; set; }
        public required string OwnerName { get; set; }
        public required string ContactPhone { get; set; }
        public decimal? CommissionPercent { get; set; }
        public required string Status { get; set; }
    }
}
