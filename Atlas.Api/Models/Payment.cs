
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Atlas.Api.Models
{
    public class Payment : ITenantOwnedEntity
    {
        public int Id { get; set; }

        public int TenantId { get; set; }
        public Tenant Tenant { get; set; } = null!;
        
        [Required]
        public int BookingId { get; set; }
        
        [ForeignKey(nameof(BookingId))]
        public Booking Booking { get; set; } = null!;
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? BaseAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? DiscountAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ConvenienceFeeAmount { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Method { get; set; } = null!;
        
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = null!;
        
        [Required]
        public DateTime ReceivedOn { get; set; }
        
        [MaxLength(500)]
        public string? Note { get; set; }
        
        // Razorpay specific fields
        [MaxLength(100)]
        public string? RazorpayOrderId { get; set; }
        
        [MaxLength(100)]
        public string? RazorpayPaymentId { get; set; }
        
        [MaxLength(200)]
        public string? RazorpaySignature { get; set; }
        
        [MaxLength(20)]
        public string Status { get; set; } = "pending";
    }
}
