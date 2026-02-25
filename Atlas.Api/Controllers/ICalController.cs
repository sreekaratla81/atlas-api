using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Atlas.Api.Controllers;

[ApiController]
[Produces("application/json")]
public class ICalController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<ICalController> _logger;

    public ICalController(AppDbContext db, ILogger<ICalController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── iCal feed export (public, no auth) ──────────────────────────

    /// <summary>Public iCal feed for a listing. Returns .ics with bookings and manual blocks.</summary>
    [HttpGet("/listings/{listingId:int}/ical")]
    [AllowAnonymous]
    [Produces("text/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportIcal(int listingId, CancellationToken ct)
    {
        var listing = await _db.Listings.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == listingId, ct);

        if (listing is null)
            return NotFound(new { error = "Listing not found." });

        var bookings = await _db.Bookings.IgnoreQueryFilters()
            .Include(b => b.Guest)
            .AsNoTracking()
            .Where(b => b.ListingId == listingId
                && b.BookingStatus != "Cancelled"
                && b.BookingStatus != "Expired")
            .ToListAsync(ct);

        var blocks = await _db.AvailabilityBlocks.IgnoreQueryFilters()
            .AsNoTracking()
            .Where(ab => ab.ListingId == listingId
                && !ab.Inventory
                && ab.BookingId == null
                && (ab.Status == BlockStatuses.Active || ab.Status == BlockStatuses.Blocked))
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCALENDAR");
        sb.AppendLine("VERSION:2.0");
        sb.AppendLine("PRODID:-//Atlas Homestays//Atlas PMS//EN");
        sb.AppendLine("CALSCALE:GREGORIAN");
        sb.AppendLine("METHOD:PUBLISH");
        sb.AppendLine($"X-WR-CALNAME:{EscapeIcal(listing.Name)}");

        foreach (var b in bookings)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"DTSTART;VALUE=DATE:{b.CheckinDate:yyyyMMdd}");
            sb.AppendLine($"DTEND;VALUE=DATE:{b.CheckoutDate:yyyyMMdd}");
            sb.AppendLine($"UID:booking-{b.Id}@atlashomestays.com");
            sb.AppendLine($"SUMMARY:{EscapeIcal(b.Guest?.Name ?? "Reserved")}");
            sb.AppendLine($"DESCRIPTION:Booking #{b.Id} via {b.BookingSource ?? "Direct"}");
            sb.AppendLine("STATUS:CONFIRMED");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine("END:VEVENT");
        }

        foreach (var blk in blocks)
        {
            sb.AppendLine("BEGIN:VEVENT");
            sb.AppendLine($"DTSTART;VALUE=DATE:{blk.StartDate:yyyyMMdd}");
            sb.AppendLine($"DTEND;VALUE=DATE:{blk.EndDate:yyyyMMdd}");
            sb.AppendLine($"UID:block-{blk.Id}@atlashomestays.com");
            sb.AppendLine("SUMMARY:Blocked");
            sb.AppendLine($"DESCRIPTION:{EscapeIcal(blk.Source)} block ({blk.BlockType})");
            sb.AppendLine("STATUS:CONFIRMED");
            sb.AppendLine($"DTSTAMP:{DateTime.UtcNow:yyyyMMddTHHmmssZ}");
            sb.AppendLine("END:VEVENT");
        }

        sb.AppendLine("END:VCALENDAR");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/calendar", $"listing-{listingId}.ics");
    }

    // ── External calendar CRUD (authorized) ─────────────────────────

    /// <summary>List external iCal calendars linked to a listing.</summary>
    [HttpGet("/api/listings/{listingId:int}/external-calendars")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListExternalCalendars(int listingId, CancellationToken ct)
    {
        var calendars = await _db.ListingExternalCalendars
            .AsNoTracking()
            .Where(c => c.ListingId == listingId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new
            {
                c.Id,
                c.ListingId,
                c.Name,
                c.ICalUrl,
                c.LastSyncAtUtc,
                c.LastSyncError,
                c.SyncedEventCount,
                c.IsActive,
                c.CreatedAtUtc
            })
            .ToListAsync(ct);

        return Ok(calendars);
    }

    /// <summary>Add an external iCal calendar URL to a listing.</summary>
    [HttpPost("/api/listings/{listingId:int}/external-calendars")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddExternalCalendar(
        int listingId,
        [FromBody] AddExternalCalendarRequest request,
        CancellationToken ct)
    {
        var listingExists = await _db.Listings.AsNoTracking().AnyAsync(l => l.Id == listingId, ct);
        if (!listingExists)
            return NotFound(new { error = "Listing not found." });

        if (!Uri.TryCreate(request.ICalUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest(new { error = "ICalUrl must be a valid HTTP(S) URL." });

        var calendar = new ListingExternalCalendar
        {
            ListingId = listingId,
            Name = request.Name?.Trim() ?? uri.Host,
            ICalUrl = request.ICalUrl.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.ListingExternalCalendars.Add(calendar);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListExternalCalendars), new { listingId }, new
        {
            calendar.Id,
            calendar.ListingId,
            calendar.Name,
            calendar.ICalUrl,
            calendar.IsActive,
            calendar.CreatedAtUtc
        });
    }

    /// <summary>Remove an external iCal calendar.</summary>
    [HttpDelete("/api/external-calendars/{id:int}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExternalCalendar(int id, CancellationToken ct)
    {
        var calendar = await _db.ListingExternalCalendars.FindAsync(new object[] { id }, ct);
        if (calendar is null)
            return NotFound(new { error = "External calendar not found." });

        var icalBlocks = await _db.AvailabilityBlocks
            .Where(ab => ab.ListingId == calendar.ListingId
                && ab.Source == AvailabilityConstants.Sources.ICal
                && ab.BlockType == calendar.Id.ToString())
            .ToListAsync(ct);

        if (icalBlocks.Count > 0)
            _db.AvailabilityBlocks.RemoveRange(icalBlocks);

        _db.ListingExternalCalendars.Remove(calendar);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Manually trigger sync for one external calendar.</summary>
    [HttpPost("/api/external-calendars/{id:int}/sync")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SyncExternalCalendar(
        int id,
        [FromServices] IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var calendar = await _db.ListingExternalCalendars.FindAsync(new object[] { id }, ct);
        if (calendar is null)
            return NotFound(new { error = "External calendar not found." });

        var httpClient = httpClientFactory.CreateClient("iCalSync");

        try
        {
            var (events, error) = await ICalSyncHelper.FetchAndSync(
                _db, httpClient, calendar, _logger, ct);

            return Ok(new
            {
                calendar.Id,
                calendar.LastSyncAtUtc,
                calendar.SyncedEventCount,
                calendar.LastSyncError,
                syncedEvents = events
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual iCal sync failed for calendar {CalendarId}", id);
            return StatusCode(500, new { error = "Sync failed. " + ex.Message });
        }
    }

    private static string EscapeIcal(string s) =>
        s.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");
}

public class AddExternalCalendarRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [Required]
    [MaxLength(2000)]
    public string ICalUrl { get; set; } = "";
}
