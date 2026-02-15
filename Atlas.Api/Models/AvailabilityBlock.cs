using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class AvailabilityBlock : ITenantOwnedEntity
    {
        public long Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;

        [Required]
        [ForeignKey(nameof(Listing))]
        public int ListingId { get; set; }

        public Listing Listing { get; set; } = null!;

        [ForeignKey(nameof(Booking))]
        public int? BookingId { get; set; }

        public Booking? Booking { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Required]
        [MaxLength(30)]
        [Column(TypeName = "varchar(30)")]
        public string BlockType { get; set; } = "Booking";

        [Required]
        [MaxLength(30)]
        [Column(TypeName = "varchar(30)")]
        public string Source { get; set; } = "System";

        [Required]
        [MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Status { get; set; } = "Active";


        [Required]
        [Column(TypeName = "bit")]
        public bool Inventory { get; set; } = true;  

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
