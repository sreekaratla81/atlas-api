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

        /// <summary>Optional. Guest info from checkout form; used to ensure WhatsApp/SMS go to the number entered at payment time.</summary>
        public GuestInfoDto? GuestInfo { get; set; }
    }
}
