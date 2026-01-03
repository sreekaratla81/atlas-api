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
}
