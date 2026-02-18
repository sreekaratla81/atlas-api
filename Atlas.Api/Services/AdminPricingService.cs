using Atlas.Api.Data;
using Atlas.Api.Data.Repositories;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Atlas.Api.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services;

public class AdminPricingService : IAdminPricingService
{
    private readonly AppDbContext _db;
    private readonly IListingPricingRepository _pricingRepo;
    private readonly IListingDailyRateRepository _dailyRateRepo;
    private readonly IListingDailyInventoryRepository _dailyInventoryRepo;
    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ITenantPricingSettingsService _tenantPricingSettings;

    public AdminPricingService(
        AppDbContext db,
        IListingPricingRepository pricingRepo,
        IListingDailyRateRepository dailyRateRepo,
        IListingDailyInventoryRepository dailyInventoryRepo,
        ITenantContextAccessor tenantAccessor,
        ITenantPricingSettingsService tenantPricingSettings)
    {
        _db = db;
        _pricingRepo = pricingRepo;
        _dailyRateRepo = dailyRateRepo;
        _dailyInventoryRepo = dailyInventoryRepo;
        _tenantAccessor = tenantAccessor;
        _tenantPricingSettings = tenantPricingSettings;
    }

    public async Task<CalendarPricingViewDto> GetCalendarPricingViewAsync(DateTime startDate, DateTime endDate, IReadOnlyList<int>? listingIds, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        var ids = listingIds != null && listingIds.Count > 0
            ? listingIds
            : await _db.Listings.AsNoTracking().Select(l => l.Id).ToListAsync(cancellationToken);

        if (ids.Count == 0)
        {
            return new CalendarPricingViewDto { StartDate = start, EndDate = end };
        }

        var tenantSettings = await _tenantPricingSettings.GetCurrentAsync(cancellationToken);
        var globalDiscountPercent = tenantSettings.GlobalDiscountPercent;
        var convenienceFeePercent = tenantSettings.ConvenienceFeePercent;

        var listings = await _db.Listings
            .AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.Name })
            .ToListAsync(cancellationToken);

        var pricingList = await _pricingRepo.GetByListingIdsAsync(ids, cancellationToken);
        var dailyRates = await _dailyRateRepo.GetForListingsInRangeAsync(ids, start, end, cancellationToken);
        var dailyInventories = await _dailyInventoryRepo.GetForListingsInRangeAsync(ids, start, end, cancellationToken);

        var pricingByListing = pricingList.ToDictionary(p => p.ListingId);
        var ratesByListingDate = dailyRates
            .GroupBy(r => r.ListingId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Date.Date, x => x.NightlyRate));
        var inventoryByListingDate = dailyInventories
            .GroupBy(i => i.ListingId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Date.Date, x => x.RoomsAvailable));

        var result = new CalendarPricingViewDto { StartDate = start, EndDate = end };

        foreach (var listing in listings)
        {
            pricingByListing.TryGetValue(listing.Id, out var pricing);
            ratesByListingDate.TryGetValue(listing.Id, out var overrideRates);
            inventoryByListingDate.TryGetValue(listing.Id, out var inventories);

            // Base and weekend rates always from ListingPricing table (fallback when no daily override)
            var baseRate = pricing?.BaseNightlyRate ?? 0;
            var weekendRate = pricing?.WeekendNightlyRate ?? pricing?.BaseNightlyRate ?? 0;
            var currency = pricing?.Currency ?? "INR";

            var days = new List<CalendarPricingDayDto>();
            for (var d = start; d < end; d = d.AddDays(1))
            {
                var baseForDay = ResolveBaseRate(baseRate, weekendRate, d);
                decimal? overrideRate = null;
                if (overrideRates != null && overrideRates.TryGetValue(d, out var ov))
                    overrideRate = ov;

                decimal baseAmount;
                decimal discountAmount;
                decimal finalAmount;

                if (overrideRate.HasValue)
                {
                    // DailyRate exists: use it as-is (no discount)
                    baseAmount = overrideRate.Value;
                    discountAmount = 0;
                    finalAmount = baseAmount;
                }
                else if (globalDiscountPercent > 0)
                {
                    // No daily rate: use BaseNightlyRate - Discount
                    baseAmount = baseForDay;
                    discountAmount = baseAmount * globalDiscountPercent / 100m;
                    finalAmount = Math.Max(0, baseAmount - discountAmount);
                }
                else
                {
                    // No daily rate, no discount: use BaseNightlyRate
                    baseAmount = baseForDay;
                    discountAmount = 0;
                    finalAmount = baseForDay;
                }

                var rooms = inventories != null && inventories.TryGetValue(d, out var r) ? r : 1;

                days.Add(new CalendarPricingDayDto
                {
                    Date = d.ToString("yyyy-MM-dd"),
                    BaseAmount = baseAmount,
                    DiscountAmount = discountAmount,
                    ConvenienceFeePercent = convenienceFeePercent,
                    FinalAmount = finalAmount,
                    GlobalDiscountPercent = globalDiscountPercent,
                    RoomsAvailable = rooms
                });
            }

            result.Listings.Add(new CalendarPricingListingDto
            {
                ListingId = listing.Id,
                ListingName = listing.Name,
                Currency = currency,
                BaseNightlyRate = baseRate,
                WeekendNightlyRate = pricing?.WeekendNightlyRate,
                Days = days
            });
        }

        return result;
    }

    public async Task<(ListingPricing Entity, bool Created)> UpdateBasePricingAsync(UpdateBasePricingDto dto, CancellationToken cancellationToken = default)
    {
        var existing = await _db.ListingPricings.FindAsync(new object[] { dto.ListingId }, cancellationToken);

        if (existing != null)
        {
            existing.BaseNightlyRate = dto.BaseNightlyRate;
            existing.WeekendNightlyRate = dto.WeekendNightlyRate;
            existing.ExtraGuestRate = dto.ExtraGuestRate;
            existing.Currency = dto.Currency;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return (existing, false);
        }

        var listingExists = await _db.Listings.AnyAsync(l => l.Id == dto.ListingId, cancellationToken);
        if (!listingExists)
            throw new InvalidOperationException($"Listing {dto.ListingId} not found.");

        var entity = new ListingPricing
        {
            ListingId = dto.ListingId,
            BaseNightlyRate = dto.BaseNightlyRate,
            WeekendNightlyRate = dto.WeekendNightlyRate,
            ExtraGuestRate = dto.ExtraGuestRate,
            Currency = dto.Currency,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _db.ListingPricings.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return (entity, true);
    }

    public async Task<ListingDailyRate> UpsertDailyRateAsync(UpsertDailyRateDto dto, int? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = new ListingDailyRate
        {
            ListingId = dto.ListingId,
            Date = dto.Date.Date,
            NightlyRate = dto.NightlyRate,
            Currency = dto.Currency,
            Source = dto.Source,
            Reason = dto.Reason,
            UpdatedByUserId = updatedByUserId
        };
        return await _dailyRateRepo.UpsertAsync(entity, cancellationToken);
    }

    public async Task<ListingDailyInventory> UpsertDailyInventoryAsync(UpsertDailyInventoryDto dto, int? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var entity = new ListingDailyInventory
        {
            ListingId = dto.ListingId,
            Date = dto.Date.Date,
            RoomsAvailable = dto.RoomsAvailable,
            Source = dto.Source,
            Reason = dto.Reason,
            UpdatedByUserId = updatedByUserId
        };
        return await _dailyInventoryRepo.UpsertAsync(entity, cancellationToken);
    }

    private static decimal ResolveBaseRate(decimal baseRate, decimal weekendRate, DateTime date)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return isWeekend ? weekendRate : baseRate;
    }
}
