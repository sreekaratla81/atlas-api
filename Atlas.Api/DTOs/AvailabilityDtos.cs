namespace Atlas.Api.DTOs
{
    public class AvailabilityResponseDto
    {
        public int PropertyId { get; set; }
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; }
        public bool IsGenericAvailable { get; set; }
        public List<AvailabilityListingDto> Listings { get; set; } = new();
    }

    public class AvailabilityListingDto
    {
        public int ListingId { get; set; }
        public string ListingName { get; set; } = string.Empty;
        public int MaxGuests { get; set; }
        public string Currency { get; set; } = string.Empty;
        public decimal TotalPrice { get; set; }
        public List<AvailabilityNightlyRateDto> NightlyRates { get; set; } = new();
    }

    public class AvailabilityNightlyRateDto
    {
        public DateTime Date { get; set; }
        public decimal Price { get; set; }
    }
}
