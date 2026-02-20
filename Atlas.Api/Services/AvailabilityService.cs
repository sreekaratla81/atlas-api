using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services
{
    public class AvailabilityService
    {
        private const string ActiveAvailabilityStatus = "Active";
        private readonly AppDbContext _context;

        public AvailabilityService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AvailabilityResponseDto> GetAvailabilityAsync(int propertyId, DateTime checkIn, DateTime checkOut, int guests)
        {
            var startDate = checkIn.Date;
            var endDate = checkOut.Date;
            var totalNights = (endDate - startDate).Days;

            if (totalNights <= 0)
            {
                throw new ArgumentException("Checkout must be after check-in.");
            }

            var listings = await _context.Listings
                .AsNoTracking()
                .Where(l => l.PropertyId == propertyId && l.MaxGuests >= guests)
                .ToListAsync();

            var listingIds = listings.Select(l => l.Id).ToList();

            if (listingIds.Count == 0)
            {
                return new AvailabilityResponseDto
                {
                    PropertyId = propertyId,
                    CheckIn = startDate,
                    CheckOut = endDate,
                    Guests = guests,
                    IsGenericAvailable = false
                };
            }

            var pricingList = await _context.ListingPricings
                .AsNoTracking()
                .Where(p => listingIds.Contains(p.ListingId))
                .ToListAsync();

            var pricingLookup = pricingList.ToDictionary(p => p.ListingId);

            var overrideRates = await _context.ListingDailyRates
                .AsNoTracking()
                .Where(o => listingIds.Contains(o.ListingId) && o.Date >= startDate && o.Date < endDate)
                .ToListAsync();

            var overrideLookup = overrideRates
                .GroupBy(o => o.ListingId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(o => o.Date.Date, o => o.NightlyRate)
                );

            var blockedListingIds = await _context.AvailabilityBlocks
                .AsNoTracking()
                .Where(block => listingIds.Contains(block.ListingId)
                                && block.Status == ActiveAvailabilityStatus
                                && block.StartDate < endDate
                                && block.EndDate > startDate)
                .Select(block => block.ListingId)
                .Distinct()
                .ToListAsync();

            var blockedSet = blockedListingIds.ToHashSet();
            var availabilityListings = new List<AvailabilityListingDto>();

            foreach (var listing in listings)
            {
                if (blockedSet.Contains(listing.Id))
                {
                    continue;
                }

                if (!pricingLookup.TryGetValue(listing.Id, out var pricing))
                {
                    continue;
                }

                var nightlyRates = new List<AvailabilityNightlyRateDto>();
                var totalPrice = 0m;
                overrideLookup.TryGetValue(listing.Id, out var listingOverrides);

                for (var i = 0; i < totalNights; i++)
                {
                    var date = startDate.AddDays(i);
                    var price = ResolveBaseRate(pricing, date);

                    if (listingOverrides != null && listingOverrides.TryGetValue(date, out var overridePrice))
                    {
                        price = overridePrice;
                    }

                    nightlyRates.Add(new AvailabilityNightlyRateDto
                    {
                        Date = date,
                        Price = price
                    });
                    totalPrice += price;
                }

                availabilityListings.Add(new AvailabilityListingDto
                {
                    ListingId = listing.Id,
                    ListingName = listing.Name,
                    MaxGuests = listing.MaxGuests,
                    Currency = pricing.Currency,
                    TotalPrice = totalPrice,
                    NightlyRates = nightlyRates
                });
            }

            return new AvailabilityResponseDto
            {
                PropertyId = propertyId,
                CheckIn = startDate,
                CheckOut = endDate,
                Guests = guests,
                IsGenericAvailable = availabilityListings.Count > 0,
                Listings = availabilityListings
            };
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
