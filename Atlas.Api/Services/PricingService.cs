using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Services
{
    public class PricingService
    {
        private readonly AppDbContext _context;
        private readonly ITenantPricingSettingsService _tenantPricingSettingsService;
        private readonly ILogger<PricingService> _logger;

        public PricingService(AppDbContext context, ITenantPricingSettingsService tenantPricingSettingsService, ILogger<PricingService> logger)
        {
            _context = context;
            _tenantPricingSettingsService = tenantPricingSettingsService;
            _logger = logger;
        }

        public async Task<PricingQuoteDto> GetPricingAsync(int listingId, DateTime checkIn, DateTime checkOut)
        {
            var (pricing, nightlyRates) = await GetBasePricingAsync(listingId, checkIn, checkOut);
            return new PricingQuoteDto
            {
                ListingId = listingId,
                Currency = pricing.Currency,
                TotalPrice = nightlyRates.Sum(x => x.Rate),
                NightlyRates = nightlyRates
            };
        }

        public async Task<PriceBreakdownDto> GetPublicBreakdownAsync(int listingId, DateTime checkIn, DateTime checkOut, string feeMode = "CustomerPays")
        {
            var (pricing, nightlyRates) = await GetBasePricingAsync(listingId, checkIn, checkOut);
            var baseAmount = nightlyRates.Sum(x => x.Rate);

            var nights = (checkOut.Date - checkIn.Date).Days;
            var ruleDiscount = await GetPricingRuleDiscountAsync(listingId, checkIn.Date, nights);
            if (ruleDiscount.CombinedMultiplier < 1m)
                baseAmount = RoundCurrency(baseAmount * ruleDiscount.CombinedMultiplier);

            var settings = await _tenantPricingSettingsService.GetCurrentAsync();
            return BuildBreakdown(listingId, pricing.Currency, baseAmount, settings, "Public", feeMode, applyGlobalDiscount: true);
        }

        /// <summary>
        /// Resolves the best LOS and Seasonal pricing rules for a stay and returns
        /// a combined multiplicative discount multiplier (e.g. 0.85 means 15% off).
        /// </summary>
        public async Task<PricingRuleDiscountResult> GetPricingRuleDiscountAsync(int listingId, DateTime checkInDate, int nights)
        {
            var rules = await _context.ListingPricingRules
                .AsNoTracking()
                .Where(r => r.ListingId == listingId && r.IsActive)
                .ToListAsync();

            var losRule = rules
                .Where(r => r.RuleType == "LOS"
                    && r.MinNights.HasValue && r.MinNights.Value <= nights
                    && (!r.MaxNights.HasValue || r.MaxNights.Value >= nights))
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();

            var seasonalRule = rules
                .Where(r => r.RuleType == "Seasonal"
                    && r.SeasonStart.HasValue && r.SeasonEnd.HasValue
                    && checkInDate >= r.SeasonStart.Value
                    && checkInDate <= r.SeasonEnd.Value)
                .OrderByDescending(r => r.Priority)
                .FirstOrDefault();

            var losMultiplier = losRule is not null ? (1m - losRule.DiscountPercent / 100m) : 1m;
            var seasonalMultiplier = seasonalRule is not null ? (1m - seasonalRule.DiscountPercent / 100m) : 1m;

            return new PricingRuleDiscountResult
            {
                LosDiscountPercent = losRule?.DiscountPercent ?? 0m,
                LosRuleId = losRule?.Id,
                SeasonalDiscountPercent = seasonalRule?.DiscountPercent ?? 0m,
                SeasonalRuleId = seasonalRule?.Id,
                CombinedMultiplier = losMultiplier * seasonalMultiplier
            };
        }

        public static PriceBreakdownDto BuildBreakdown(
            int listingId,
            string currency,
            decimal baseAmount,
            TenantPricingSetting settings,
            string pricingSource,
            string feeMode,
            bool applyGlobalDiscount,
            string? nonce = null,
            DateTime? quoteExpiresAtUtc = null)
        {
            var normalizedFeeMode = string.Equals(feeMode, "Absorb", StringComparison.OrdinalIgnoreCase) ? "Absorb" : "CustomerPays";
            var discountPercent = applyGlobalDiscount ? settings.GlobalDiscountPercent : 0m;

            var discountAmount = RoundCurrency(baseAmount * discountPercent / 100m);
            var discountedSubtotal = Math.Max(0, baseAmount - discountAmount);
            var convenienceFee = normalizedFeeMode == "CustomerPays"
                ? RoundCurrency(discountedSubtotal * settings.ConvenienceFeePercent / 100m)
                : 0m;

            return new PriceBreakdownDto
            {
                ListingId = listingId,
                Currency = currency,
                BaseAmount = RoundCurrency(baseAmount),
                DiscountAmount = discountAmount,
                ConvenienceFeeAmount = convenienceFee,
                FinalAmount = RoundCurrency(discountedSubtotal + convenienceFee),
                ConvenienceFeePercent = settings.ConvenienceFeePercent,
                GlobalDiscountPercent = discountPercent,
                PricingSource = pricingSource,
                FeeMode = normalizedFeeMode,
                QuoteTokenNonce = nonce,
                QuoteExpiresAtUtc = quoteExpiresAtUtc
            };
        }

        public static decimal RoundCurrency(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

        private async Task<(ListingPricing Pricing, List<PricingNightlyRateDto> NightlyRates)> GetBasePricingAsync(int listingId, DateTime checkIn, DateTime checkOut)
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
                .ToDictionary(group => group.Key, group => group.First().NightlyRate);

            const decimal MaxSaneNightlyRate = 500_000m;
            var nightlyRates = new List<PricingNightlyRateDto>();

            for (var i = 0; i < totalNights; i++)
            {
                var date = startDate.AddDays(i);
                var baseRate = ResolveBaseRate(pricing, date);

                if (overrideLookup.TryGetValue(date, out var overrideRate))
                {
                    baseRate = overrideRate;
                }

                if (baseRate < 0 || baseRate > MaxSaneNightlyRate)
                {
                    _logger.LogWarning("Listing {ListingId} has an invalid nightly rate {Rate} on {Date}; clamping to 0",
                        listingId, baseRate, date.ToString("yyyy-MM-dd"));
                    baseRate = 0;
                }

                nightlyRates.Add(new PricingNightlyRateDto
                {
                    Date = date,
                    Rate = baseRate
                });
            }

            return (pricing, nightlyRates);
        }

        private static decimal ResolveBaseRate(ListingPricing pricing, DateTime date)
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday;

            if (isWeekend)
            {
                return pricing.WeekendNightlyRate ?? pricing.BaseNightlyRate;
            }

            return pricing.BaseNightlyRate;
        }
    }
}
