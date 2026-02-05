using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.DTOs
{
    public class PaymentCreateDto
    {
        [Required]
        public int BookingId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [MaxLength(50)]
        public string Method { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [Required]
        public DateTime ReceivedOn { get; set; }

        [Required]
        public string Note { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? RazorpayOrderId { get; set; }

        [MaxLength(100)]
        public string? RazorpayPaymentId { get; set; }

        [MaxLength(200)]
        public string? RazorpaySignature { get; set; }

        [MaxLength(20)]
        public string? Status { get; set; } = "pending";
    }
}
