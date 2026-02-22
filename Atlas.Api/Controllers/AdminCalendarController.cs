using System.Security.Cryptography;
using System.Text.Json;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Controllers;

/// <summary>Calendar view of availability and inventory for admin portal.</summary>
[ApiController]
[Route("admin/calendar")]
[Produces("application/json")]
[Authorize]
public class AdminCalendarController : ControllerBase
{
    private const string UpsertEventType = "AdminCalendarAvailabilityUpsert";
    private readonly AppDbContext _context;

    public AdminCalendarController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("availability")]
    [ProducesResponseType(typeof(IEnumerable<AdminCalendarAvailabilityCellDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<AdminCalendarAvailabilityCellDto>>> GetAvailability(
        [FromQuery] int propertyId,
        [FromQuery] DateTime from,
        [FromQuery] int days = 30,
        [FromQuery] int? listingId = null)
    {
        if (propertyId <= 0)
        {
            ModelState.AddModelError(nameof(propertyId), "PropertyId must be greater than 0.");
            return BadRequest(ModelState);
        }

        if (days <= 0)
        {
            ModelState.AddModelError(nameof(days), "Days must be greater than 0.");
            return BadRequest(ModelState);
        }

        var startDate = from.Date;
        var endDate = startDate.AddDays(days);

        var listingsQuery = _context.Listings
            .AsNoTracking()
            .Where(l => l.PropertyId == propertyId);

        if (listingId.HasValue)
        {
            listingsQuery = listingsQuery.Where(l => l.Id == listingId.Value);
        }

        var listings = await listingsQuery
            .Select(l => new { l.Id })
            .ToListAsync();

        if (listingId.HasValue && listings.Count == 0)
        {
            return NotFound();
        }

        var listingIds = listings.Select(l => l.Id).ToArray();
        var cells = await BuildAvailabilityCellsAsync(listingIds, startDate, endDate);
        return Ok(cells);
    }

    [HttpPut("availability")]
    [ProducesResponseType(typeof(AdminCalendarAvailabilityBulkUpsertResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminCalendarAvailabilityBulkUpsertResponseDto>> UpsertAvailability(
        [FromBody] AdminCalendarAvailabilityBulkUpsertRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        for (var i = 0; i < request.Cells.Count; i++)
        {
            var cell = request.Cells[i];
            if (cell.RoomsAvailable < 0)
            {
                ModelState.AddModelError($"cells[{i}].roomsAvailable", "RoomsAvailable must be greater than or equal to 0.");
            }

            if (cell.PriceOverride.HasValue && cell.PriceOverride.Value < 0)
            {
                ModelState.AddModelError($"cells[{i}].priceOverride", "PriceOverride must be greater than or equal to 0.");
            }
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var uniqueListingIds = request.Cells.Select(c => c.ListingId).Distinct().ToArray();
        var visibleListings = await _context.Listings
            .AsNoTracking()
            .Where(l => uniqueListingIds.Contains(l.Id))
            .Select(l => new { l.Id, l.TenantId })
            .ToListAsync();

        if (visibleListings.Count != uniqueListingIds.Length)
        {
            return NotFound();
        }

        var tenantId = visibleListings[0].TenantId;

        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var payloadHash = ComputeRequestHash(request);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingLog = await _context.CommunicationLogs
                .AsNoTracking()
                .OrderByDescending(l => l.CreatedAtUtc)
                .FirstOrDefaultAsync(l => l.EventType == UpsertEventType
                    && l.TenantId == tenantId
                    && l.IdempotencyKey == idempotencyKey);

            if (existingLog is not null)
            {
                if (!string.Equals(existingLog.CorrelationId, payloadHash, StringComparison.Ordinal))
                {
                    return Conflict(new { message = "Idempotency-Key was already used with a different payload." });
                }

                var dedupedCells = await BuildCellsForRequestedDatesAsync(request.Cells);
                return Ok(new AdminCalendarAvailabilityBulkUpsertResponseDto
                {
                    UpdatedCells = request.Cells.Count,
                    Deduplicated = true,
                    Cells = dedupedCells
                });
            }
        }

        var targetDates = request.Cells.Select(c => c.Date.Date).Distinct().ToArray();

        var existingInventory = await _context.ListingDailyInventories
            .Where(i => uniqueListingIds.Contains(i.ListingId) && targetDates.Contains(i.Date))
            .ToListAsync();

        var existingRates = await _context.ListingDailyRates
            .Where(r => uniqueListingIds.Contains(r.ListingId) && targetDates.Contains(r.Date))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var cell in request.Cells)
        {
            var day = cell.Date.Date;

            var inventory = existingInventory.FirstOrDefault(i => i.ListingId == cell.ListingId && i.Date == day);
            if (inventory is null)
            {
                _context.ListingDailyInventories.Add(new ListingDailyInventory
                {
                    ListingId = cell.ListingId,
                    Date = day,
                    RoomsAvailable = cell.RoomsAvailable,
                    Source = BookingSources.Manual,
                    UpdatedAtUtc = now
                });
            }
            else
            {
                inventory.RoomsAvailable = cell.RoomsAvailable;
                inventory.Source = BookingSources.Manual;
                inventory.UpdatedAtUtc = now;
            }

            var rate = existingRates.FirstOrDefault(r => r.ListingId == cell.ListingId && r.Date == day);
            if (cell.PriceOverride.HasValue)
            {
                if (rate is null)
                {
                    _context.ListingDailyRates.Add(new ListingDailyRate
                    {
                        ListingId = cell.ListingId,
                        Date = day,
                        NightlyRate = cell.PriceOverride.Value,
                        Currency = CurrencyConstants.INR,
                        Source = BookingSources.Manual,
                        UpdatedAtUtc = now
                    });
                }
                else
                {
                    rate.NightlyRate = cell.PriceOverride.Value;
                    rate.Source = BookingSources.Manual;
                    rate.UpdatedAtUtc = now;
                }
            }
            else if (rate is not null)
            {
                _context.ListingDailyRates.Remove(rate);
            }
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _context.CommunicationLogs.Add(new CommunicationLog
            {
                Channel = "api",
                EventType = UpsertEventType,
                ToAddress = "admin/calendar/availability",
                CorrelationId = payloadHash,
                IdempotencyKey = idempotencyKey,
                Provider = "internal",
                Status = "Succeeded",
                AttemptCount = 1,
                CreatedAtUtc = now
            });
        }

        await _context.SaveChangesAsync();

        var cells = await BuildCellsForRequestedDatesAsync(request.Cells);
        return Ok(new AdminCalendarAvailabilityBulkUpsertResponseDto
        {
            UpdatedCells = request.Cells.Count,
            Deduplicated = false,
            Cells = cells
        });
    }

    private async Task<List<AdminCalendarAvailabilityCellDto>> BuildCellsForRequestedDatesAsync(
        IEnumerable<AdminCalendarAvailabilityCellUpsertDto> requestedCells)
    {
        var listingIds = requestedCells.Select(c => c.ListingId).Distinct().ToArray();
        var dates = requestedCells.Select(c => c.Date.Date).Distinct().OrderBy(d => d).ToList();

        var result = new List<AdminCalendarAvailabilityCellDto>();
        foreach (var date in dates)
        {
            var dayCells = await BuildAvailabilityCellsAsync(listingIds, date, date.AddDays(1));
            result.AddRange(dayCells.Where(c => c.Date == date));
        }

        return result
            .Where(c => requestedCells.Any(rc => rc.ListingId == c.ListingId && rc.Date.Date == c.Date.Date))
            .OrderBy(c => c.Date)
            .ThenBy(c => c.ListingId)
            .ToList();
    }

    private async Task<List<AdminCalendarAvailabilityCellDto>> BuildAvailabilityCellsAsync(int[] listingIds, DateTime startDate, DateTime endDate)
    {
        if (listingIds.Length == 0)
        {
            return new List<AdminCalendarAvailabilityCellDto>();
        }

        var pricingMap = await _context.ListingPricings
            .AsNoTracking()
            .Where(p => listingIds.Contains(p.ListingId))
            .ToDictionaryAsync(p => p.ListingId);

        var rateMap = await _context.ListingDailyRates
            .AsNoTracking()
            .Where(r => listingIds.Contains(r.ListingId) && r.Date >= startDate && r.Date < endDate)
            .ToDictionaryAsync(r => (r.ListingId, r.Date.Date), r => r.NightlyRate);

        var inventoryMap = await _context.ListingDailyInventories
            .AsNoTracking()
            .Where(i => listingIds.Contains(i.ListingId) && i.Date >= startDate && i.Date < endDate)
            .ToDictionaryAsync(i => (i.ListingId, i.Date.Date), i => i.RoomsAvailable);

        var blocks = await _context.AvailabilityBlocks
            .AsNoTracking()
            .Where(b => listingIds.Contains(b.ListingId)
                && b.StartDate < endDate
                && b.EndDate > startDate
                && (b.Status == BlockStatuses.Active || b.Status == BlockStatuses.Blocked))
            .Select(b => new { b.ListingId, b.StartDate, b.EndDate, b.Inventory })
            .ToListAsync();

        var cells = new List<AdminCalendarAvailabilityCellDto>();
        foreach (var listing in listingIds)
        {
            pricingMap.TryGetValue(listing, out var pricing);

            for (var date = startDate; date < endDate; date = date.AddDays(1))
            {
                var key = (listing, date.Date);
                var isBlocked = blocks.Any(b => b.ListingId == listing && b.StartDate.Date <= date.Date && b.EndDate.Date > date.Date && !b.Inventory);
                var basePrice = (date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday)
                    ? pricing?.WeekendNightlyRate ?? pricing?.BaseNightlyRate ?? 0m
                    : pricing?.BaseNightlyRate ?? 0m;

                decimal? overridePrice = null;
                if (rateMap.TryGetValue(key, out var nightlyRate))
                {
                    overridePrice = nightlyRate;
                }

                var roomsAvailable = inventoryMap.TryGetValue(key, out var rooms)
                    ? rooms
                    : 1;

                if (isBlocked)
                {
                    roomsAvailable = 0;
                }

                cells.Add(new AdminCalendarAvailabilityCellDto
                {
                    Date = date.Date,
                    ListingId = listing,
                    RoomsAvailable = roomsAvailable,
                    EffectivePrice = overridePrice ?? basePrice,
                    PriceOverride = overridePrice,
                    IsBlocked = isBlocked
                });
            }
        }

        return cells
            .OrderBy(c => c.Date)
            .ThenBy(c => c.ListingId)
            .ToList();
    }

    private static string ComputeRequestHash(AdminCalendarAvailabilityBulkUpsertRequestDto request)
    {
        var normalized = request.Cells
            .OrderBy(c => c.ListingId)
            .ThenBy(c => c.Date.Date)
            .Select(c => new
            {
                c.ListingId,
                Date = c.Date.Date.ToString("yyyy-MM-dd"),
                c.RoomsAvailable,
                c.PriceOverride
            })
            .ToList();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(normalized);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
