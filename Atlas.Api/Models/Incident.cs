
using System;

namespace Atlas.Api.Models
{
    public class Incident
    {
        public int Id { get; set; }
        public int ListingId { get; set; }
        public int? BookingId { get; set; }
        public string Description { get; set; }
        public string ActionTaken { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
