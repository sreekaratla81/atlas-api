using Atlas.Api.DTOs;

namespace Atlas.Api.Services;

public interface IGuestPricingService
{
    /// <summary>
    /// Gets availability and nightly rate for a listing in a date range.
    /// ListingDailyRate overrides base/weekend rate. Returns roomsAvailable and isAvailable per day.
    /// </summary>
    Task<GuestAvailabilityRateResponseDto?> GetAvailabilityAndRatesAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
