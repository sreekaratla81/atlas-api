using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models.Dtos.Razorpay
{
    public class VerifyRazorpayPaymentRequest
    {
        [Required]
        public int BookingId { get; set; }
        
        [Required]
        public string RazorpayOrderId { get; set; } = null!;
        
        [Required]
        public string RazorpayPaymentId { get; set; } = null!;
        
        [Required]
        public string RazorpaySignature { get; set; } = null!;
    }
}
