namespace Atlas.Api.DTOs
{
    /// <summary>Pricing quote with nightly rate breakdown for a listing.</summary>
    public class PricingQuoteDto
    {
        public int ListingId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<PricingNightlyRateDto> NightlyRates { get; set; } = new();
    }

    /// <summary>Single night's rate within a pricing quote.</summary>
    public class PricingNightlyRateDto
    {
        public DateTime Date { get; set; }
        public decimal Rate { get; set; }
    }

    /// <summary>Detailed price breakdown including base, discount, convenience fee, and final amount.</summary>
    public class PriceBreakdownDto
    {
        public int ListingId { get; set; }
        public string Currency { get; set; } = "INR";
        public decimal BaseAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal ConvenienceFeeAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public decimal ConvenienceFeePercent { get; set; }
        public decimal GlobalDiscountPercent { get; set; }
        public string PricingSource { get; set; } = "Public";
        public string FeeMode { get; set; } = "CustomerPays";
        public DateTime? QuoteExpiresAtUtc { get; set; }
        public string? QuoteTokenNonce { get; set; }
    }

    /// <summary>Request body for computing a price breakdown.</summary>
    public class PricingBreakdownRequestDto
    {
        public int ListingId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
    }
}
