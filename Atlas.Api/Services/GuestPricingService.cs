using Atlas.Api.Data;
using Atlas.Api.Data.Repositories;
using Atlas.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services;

public class GuestPricingService : IGuestPricingService
{
    private readonly AppDbContext _db;
    private readonly IListingPricingRepository _pricingRepo;
    private readonly IListingDailyRateRepository _dailyRateRepo;
    private readonly IListingDailyInventoryRepository _dailyInventoryRepo;
    private readonly ITenantPricingSettingsService _tenantPricingSettings;

    public GuestPricingService(
        AppDbContext db,
        IListingPricingRepository pricingRepo,
        IListingDailyRateRepository dailyRateRepo,
        IListingDailyInventoryRepository dailyInventoryRepo,
        ITenantPricingSettingsService tenantPricingSettings)
    {
        _db = db;
        _pricingRepo = pricingRepo;
        _dailyRateRepo = dailyRateRepo;
        _dailyInventoryRepo = dailyInventoryRepo;
        _tenantPricingSettings = tenantPricingSettings;
    }

    public async Task<GuestAvailabilityRateResponseDto?> GetAvailabilityAndRatesAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        var listing = await _db.Listings
            .AsNoTracking()
            .Where(l => l.Id == listingId)
            .Select(l => new { l.Id, l.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (listing == null)
            return null;

        var tenantSettings = await _tenantPricingSettings.GetCurrentAsync(cancellationToken);
        var globalDiscountPercent = tenantSettings.GlobalDiscountPercent;

        var pricing = await _pricingRepo.GetByListingIdAsync(listingId, cancellationToken);
        var dailyRates = await _dailyRateRepo.GetForListingInRangeAsync(listingId, start, end, cancellationToken);
        var dailyInventories = await _dailyInventoryRepo.GetForListingInRangeAsync(listingId, start, end, cancellationToken);

        var overrideByDate = dailyRates.ToDictionary(r => r.Date.Date, r => r.NightlyRate);
        var roomsByDate = dailyInventories.ToDictionary(i => i.Date.Date, i => i.RoomsAvailable);

        var baseRate = pricing?.BaseNightlyRate ?? 0;
        var weekendRate = pricing?.WeekendNightlyRate ?? pricing?.BaseNightlyRate ?? 0;
        var currency = pricing?.Currency ?? "INR";

        const decimal MaxSaneNightlyRate = 500_000m;

        var days = new List<GuestDayAvailabilityRateDto>();
        for (var d = start; d < end; d = d.AddDays(1))
        {
            var rawRate = overrideByDate.TryGetValue(d, out var ov) ? ov : ResolveBaseRate(baseRate, weekendRate, d);
            var nightlyRate = PricingHelpers.ApplyGlobalDiscount(rawRate, globalDiscountPercent);
            var roomsAvailable = roomsByDate.TryGetValue(d, out var rooms) ? rooms : 1;
            var isAvailable = roomsAvailable > 0;

            if (nightlyRate > MaxSaneNightlyRate || nightlyRate < 0)
            {
                nightlyRate = 0;
                isAvailable = false;
            }

            days.Add(new GuestDayAvailabilityRateDto
            {
                Date = d.ToString("yyyy-MM-dd"),
                RoomsAvailable = roomsAvailable,
                IsAvailable = isAvailable,
                NightlyRate = nightlyRate
            });
        }

        return new GuestAvailabilityRateResponseDto
        {
            ListingId = listing.Id,
            ListingName = listing.Name,
            Currency = currency,
            StartDate = start,
            EndDate = end,
            Days = days
        };
    }

    private static decimal ResolveBaseRate(decimal baseRate, decimal weekendRate, DateTime date)
    {
        var isWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;
        return isWeekend ? weekendRate : baseRate;
    }
}
