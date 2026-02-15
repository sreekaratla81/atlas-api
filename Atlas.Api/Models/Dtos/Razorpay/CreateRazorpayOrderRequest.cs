using System.ComponentModel.DataAnnotations;

namespace Atlas.Api.Models.Dtos.Razorpay
{
    public class CreateRazorpayOrderRequest
    {
        public int? BookingId { get; set; }
        public BookingDraftDto? BookingDraft { get; set; }

        // Deprecated client-provided amount kept for backward compatibility only.
        public decimal? Amount { get; set; }

        [Required]
        public string Currency { get; set; } = "INR";

        [Required]
        public GuestInfoDto GuestInfo { get; set; } = null!;

        public string? QuoteToken { get; set; }
    }

    public class BookingDraftDto
    {
        public int ListingId { get; set; }
        public DateTime CheckinDate { get; set; }
        public DateTime CheckoutDate { get; set; }
        public int Guests { get; set; }
        public string? Notes { get; set; }
    }

    public class GuestInfoDto
    {
        [Required]
        public string Name { get; set; } = null!;
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        [Phone]
        public string Phone { get; set; } = null!;
    }
}
