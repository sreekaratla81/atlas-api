using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Services;

public interface IAdminPricingService
{
    Task<CalendarPricingViewDto> GetCalendarPricingViewAsync(DateTime startDate, DateTime endDate, IReadOnlyList<int>? listingIds, CancellationToken cancellationToken = default);
    Task<ListingPricing> UpdateBasePricingAsync(UpdateBasePricingDto dto, CancellationToken cancellationToken = default);
    Task<ListingDailyRate> UpsertDailyRateAsync(UpsertDailyRateDto dto, int? updatedByUserId, CancellationToken cancellationToken = default);
    Task<ListingDailyInventory> UpsertDailyInventoryAsync(UpsertDailyInventoryDto dto, int? updatedByUserId, CancellationToken cancellationToken = default);
}
