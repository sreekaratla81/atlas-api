namespace Atlas.Api.Models.Dtos.Razorpay
{
    public class RazorpayOrderResponse
    {
        public string KeyId { get; set; } = null!;
        public string OrderId { get; set; } = null!;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public int BookingId { get; set; }
    }
}
