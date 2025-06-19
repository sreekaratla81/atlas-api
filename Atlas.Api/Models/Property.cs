
namespace Atlas.Api.Models
{
    public class Property
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Type { get; set; }
        public string OwnerName { get; set; }
        public string ContactPhone { get; set; }
        public decimal? CommissionPercent { get; set; }
        public string Status { get; set; }
    }
}
