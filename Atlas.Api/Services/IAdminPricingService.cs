using Atlas.Api.DTOs;
using Atlas.Api.Models;

namespace Atlas.Api.Services;

public interface IAdminPricingService
{
    Task<CalendarPricingViewDto> GetCalendarPricingViewAsync(DateTime startDate, DateTime endDate, IReadOnlyList<int>? listingIds, CancellationToken cancellationToken = default);
    /// <summary>Upsert: if listing pricing exists, update it; otherwise create new. Returns (entity, created).</summary>
    Task<(ListingPricing Entity, bool Created)> UpdateBasePricingAsync(UpdateBasePricingDto dto, CancellationToken cancellationToken = default);
    Task<ListingDailyRate> UpsertDailyRateAsync(UpsertDailyRateDto dto, int? updatedByUserId, CancellationToken cancellationToken = default);
    Task<ListingDailyInventory> UpsertDailyInventoryAsync(UpsertDailyInventoryDto dto, int? updatedByUserId, CancellationToken cancellationToken = default);
}
