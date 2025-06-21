
using System;

namespace Atlas.Api.Models
{
    public class Payment
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public decimal Amount { get; set; }
        public string Method { get; set; }
        public string Type { get; set; }
        public DateTime ReceivedOn { get; set; }
        public string Note { get; set; }
    }
}
