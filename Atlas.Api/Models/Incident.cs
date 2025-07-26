
using System;

namespace Atlas.Api.Models
{
    public class Incident
    {
        public int Id { get; set; }
        public int ListingId { get; set; }
        public int? BookingId { get; set; }
        public required string Description { get; set; }
        public required string ActionTaken { get; set; }
        public required string Status { get; set; }
        public required string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
