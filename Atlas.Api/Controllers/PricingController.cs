using Atlas.Api.DTOs;
using Atlas.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Api.Controllers;

[ApiController]
[Route("pricing")]
[Produces("application/json")]
public class PricingController : ControllerBase
{
    private readonly IGuestPricingService _guestPricingService;
    private readonly IAdminPricingService _adminPricingService;
    private readonly PricingService _pricingService;

    public PricingController(
        IGuestPricingService guestPricingService,
        IAdminPricingService adminPricingService,
        PricingService pricingService)
    {
        _guestPricingService = guestPricingService;
        _adminPricingService = adminPricingService;
        _pricingService = pricingService;
    }

    /// <summary>
    /// Update base pricing for a listing.
    /// </summary>
    [HttpPut("base")]
    public async Task<IActionResult> UpdateBasePricing(
        [FromBody] UpdateBasePricingDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _adminPricingService.UpdateBasePricingAsync(dto, cancellationToken);
            return Ok(new
            {
                listingId = updated.ListingId,
                baseNightlyRate = updated.BaseNightlyRate,
                weekendNightlyRate = updated.WeekendNightlyRate,
                extraGuestRate = updated.ExtraGuestRate,
                currency = updated.Currency,
                updatedAtUtc = updated.UpdatedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Upsert daily rate override for a listing and date.
    /// </summary>
    [HttpPut("daily-rate")]
    public async Task<IActionResult> UpsertDailyRate(
        [FromBody] UpsertDailyRateDto dto,
        [FromQuery] int? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var updated = await _adminPricingService.UpsertDailyRateAsync(dto, updatedByUserId, cancellationToken);
        return Ok(new
        {
            listingId = updated.ListingId,
            date = updated.Date.ToString("yyyy-MM-dd"),
            nightlyRate = updated.NightlyRate,
            currency = updated.Currency,
            updatedAtUtc = updated.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Upsert daily inventory for a listing and date.
    /// </summary>
    [HttpPut("daily-inventory")]
    public async Task<IActionResult> UpsertDailyInventory(
        [FromBody] UpsertDailyInventoryDto dto,
        [FromQuery] int? updatedByUserId,
        CancellationToken cancellationToken)
    {
        var updated = await _adminPricingService.UpsertDailyInventoryAsync(dto, updatedByUserId, cancellationToken);
        return Ok(new
        {
            listingId = updated.ListingId,
            date = updated.Date.ToString("yyyy-MM-dd"),
            roomsAvailable = updated.RoomsAvailable,
            updatedAtUtc = updated.UpdatedAtUtc
        });
    }

    /// <summary>
    /// Get pricing summary for the current date only. Returns all listings with that day's baseAmount, discountAmount, convenienceFeePercent, finalAmount, globalDiscountPercent. Uses the same logic as pricing/breakdown.
    /// </summary>
    [HttpGet("daily-summary")]
    public async Task<ActionResult<DailyPricingSummaryDto>> GetDailySummary(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var calendar = await _adminPricingService.GetCalendarPricingViewAsync(today, tomorrow, null, cancellationToken);

        var listings = new List<DailyListingPricingDto>();
        foreach (var listing in calendar.Listings)
        {
            var day = listing.Days.FirstOrDefault();
            if (day == null)
                continue;

            listings.Add(new DailyListingPricingDto
            {
                ListingId = listing.ListingId,
                ListingName = listing.ListingName,
                BaseAmount = day.BaseAmount,
                DiscountAmount = day.DiscountAmount,
                ConvenienceFeePercent = day.ConvenienceFeePercent,
                FinalAmount = day.FinalAmount,
                GlobalDiscountPercent = day.GlobalDiscountPercent
            });
        }

        return Ok(new DailyPricingSummaryDto
        {
            Date = today.ToString("yyyy-MM-dd"),
            Listings = listings
        });
    }

    /// <summary>
    /// Guest: Get pricing breakdown (BaseAmount, DiscountAmount, ConvenienceFeeAmount, FinalAmount) for listingId, checkIn, checkOut.
    /// Admin: Get calendar pricing view for startDate, listingId, months (1-12).
    /// </summary>
    [HttpGet("breakdown")]
    public async Task<IActionResult> GetBreakdown(
        [FromQuery] string? startDate,
        [FromQuery] string? checkIn,
        [FromQuery] string? checkOut,
        [FromQuery] int listingId,
        [FromQuery] int months = 1,
        CancellationToken cancellationToken = default)
    {
        const string dateFormat = "yyyy-MM-dd";

        if (listingId <= 0)
        {
            ModelState.AddModelError(nameof(listingId), "Listing ID must be greater than 0.");
            return BadRequest(ModelState);
        }

        // Guest path: checkIn + checkOut
        if (!string.IsNullOrWhiteSpace(checkIn) && !string.IsNullOrWhiteSpace(checkOut))
        {
            if (!DateTime.TryParseExact(checkIn.Trim(), dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var ci))
            {
                ModelState.AddModelError(nameof(checkIn), "Check-in must be a valid date in yyyy-MM-dd format.");
                return BadRequest(ModelState);
            }
            if (!DateTime.TryParseExact(checkOut.Trim(), dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var co))
            {
                ModelState.AddModelError(nameof(checkOut), "Check-out must be a valid date in yyyy-MM-dd format.");
                return BadRequest(ModelState);
            }
            if (co.Date <= ci.Date)
            {
                ModelState.AddModelError(nameof(checkOut), "Check-out must be after check-in.");
                return BadRequest(ModelState);
            }
            try
            {
                var breakdown = await _pricingService.GetPublicBreakdownAsync(listingId, ci, co, "CustomerPays");
                return Ok(breakdown);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // Admin path: startDate + months
        if (string.IsNullOrWhiteSpace(startDate))
        {
            ModelState.AddModelError(nameof(startDate), "Start date is required (yyyy-MM-dd), or provide checkIn and checkOut for guest breakdown.");
            return BadRequest(ModelState);
        }

        if (months < 1 || months > 12)
        {
            ModelState.AddModelError(nameof(months), "Months must be between 1 and 12.");
            return BadRequest(ModelState);
        }

        if (!DateTime.TryParseExact(startDate.Trim(), dateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start))
        {
            ModelState.AddModelError(nameof(startDate), "Start date must be a valid date in yyyy-MM-dd format.");
            return BadRequest(ModelState);
        }

        var end = start.Date.AddMonths(months);
        var ids = new[] { listingId };
        var result = await _adminPricingService.GetCalendarPricingViewAsync(start, end, ids, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Guest: Get availability and nightly rate for a listing in a date range.
    /// ListingDailyRate overrides base/weekend rate. Returns roomsAvailable and isAvailable per day.
    /// </summary>
    [HttpGet("availability-rates")]
    public async Task<ActionResult<GuestAvailabilityRateResponseDto>> GetAvailabilityAndRates(
        [FromQuery] int listingId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        if (listingId <= 0)
        {
            ModelState.AddModelError(nameof(listingId), "Listing ID must be greater than 0.");
            return BadRequest(ModelState);
        }

        if (endDate.Date <= startDate.Date)
        {
            ModelState.AddModelError(nameof(endDate), "End date must be after start date.");
            return BadRequest(ModelState);
        }

        var result = await _guestPricingService.GetAvailabilityAndRatesAsync(listingId, startDate, endDate, cancellationToken);
        if (result == null)
            return NotFound(new { message = "Listing not found." });

        return Ok(result);
    }
}
