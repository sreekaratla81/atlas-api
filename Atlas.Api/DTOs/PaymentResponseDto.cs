namespace Atlas.Api.DTOs;

/// <summary>Payment data returned by the API. Excludes RazorpaySignature for security.</summary>
public class PaymentResponseDto
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public decimal Amount { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? ConvenienceFeeAmount { get; set; }
    public string Method { get; set; } = null!;
    public string Type { get; set; } = null!;
    public DateTime ReceivedOn { get; set; }
    public string? Note { get; set; }
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayPaymentId { get; set; }
    public string Status { get; set; } = null!;
}
