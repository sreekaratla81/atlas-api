namespace Atlas.Api.DTOs
{
    public class PricingQuoteDto
    {
        public int ListingId { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<PricingNightlyRateDto> NightlyRates { get; set; } = new();
    }

    public class PricingNightlyRateDto
    {
        public DateTime Date { get; set; }
        public decimal Rate { get; set; }
    }

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

    public class PricingBreakdownRequestDto
    {
        public int ListingId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
    }
}
