using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services
{
    public class PricingService
    {
        private readonly AppDbContext _context;

        public PricingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PricingQuoteDto> GetPricingAsync(int listingId, DateTime checkIn, DateTime checkOut)
        {
            var startDate = checkIn.Date;
            var endDate = checkOut.Date;
            var totalNights = (endDate - startDate).Days;

            if (totalNights <= 0)
            {
                throw new ArgumentException("Checkout must be after check-in.");
            }

            var pricing = await _context.ListingPricings
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.ListingId == listingId);

            if (pricing is null)
            {
                throw new InvalidOperationException("Pricing is not configured for this listing.");
            }

            var overrides = await _context.ListingDailyRates
                .AsNoTracking()
                .Where(r => r.ListingId == listingId && r.Date >= startDate && r.Date < endDate)
                .ToListAsync();

            var overrideLookup = overrides
                .GroupBy(r => r.Date.Date)
                .ToDictionary(group => group.Key, group => group.First().Rate);

            var nightlyRates = new List<PricingNightlyRateDto>();
            var totalPrice = 0m;

            for (var i = 0; i < totalNights; i++)
            {
                var date = startDate.AddDays(i);
                var baseRate = ResolveBaseRate(pricing, date);

                if (overrideLookup.TryGetValue(date, out var overrideRate))
                {
                    baseRate = overrideRate;
                }

                nightlyRates.Add(new PricingNightlyRateDto
                {
                    Date = date,
                    Rate = baseRate
                });
                totalPrice += baseRate;
            }

            return new PricingQuoteDto
            {
                ListingId = listingId,
                Currency = pricing.Currency,
                TotalPrice = totalPrice,
                NightlyRates = nightlyRates
            };
        }

        private static decimal ResolveBaseRate(ListingPricing pricing, DateTime date)
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

            if (isWeekend)
            {
                return pricing.WeekendRate ?? pricing.BaseRate;
            }

            return pricing.WeekdayRate ?? pricing.BaseRate;
        }
    }
}
