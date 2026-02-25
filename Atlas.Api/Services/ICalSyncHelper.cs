using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api;

/// <summary>
/// Shared iCal fetch-and-sync logic used by both the manual sync endpoint
/// and the background hosted service.
/// </summary>
public static class ICalSyncHelper
{
    /// <summary>
    /// Downloads the iCal feed, parses VEVENT blocks, and upserts AvailabilityBlocks.
    /// Returns the count of synced events and any error message.
    /// </summary>
    public static async Task<(int EventCount, string? Error)> FetchAndSync(
        AppDbContext db,
        HttpClient httpClient,
        ListingExternalCalendar calendar,
        ILogger logger,
        CancellationToken ct)
    {
        string icsText;
        try
        {
            using var response = await httpClient.GetAsync(calendar.ICalUrl, ct);
            response.EnsureSuccessStatusCode();
            icsText = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            calendar.LastSyncAtUtc = DateTime.UtcNow;
            calendar.LastSyncError = $"HTTP fetch failed: {ex.Message}"[..Math.Min(500, $"HTTP fetch failed: {ex.Message}".Length)];
            await db.SaveChangesAsync(ct);
            return (0, calendar.LastSyncError);
        }

        var events = ParseVEvents(icsText);

        var calIdTag = calendar.Id.ToString();
        var existingBlocks = await db.AvailabilityBlocks.IgnoreQueryFilters()
            .Where(ab => ab.ListingId == calendar.ListingId
                && ab.Source == AvailabilityConstants.Sources.ICal
                && ab.BlockType == calIdTag)
            .ToListAsync(ct);

        db.AvailabilityBlocks.RemoveRange(existingBlocks);

        var now = DateTime.UtcNow;
        foreach (var ev in events)
        {
            if (ev.DtEnd <= ev.DtStart) continue;

            db.AvailabilityBlocks.Add(new AvailabilityBlock
            {
                TenantId = calendar.TenantId,
                ListingId = calendar.ListingId,
                StartDate = ev.DtStart,
                EndDate = ev.DtEnd,
                BlockType = calIdTag,
                Source = AvailabilityConstants.Sources.ICal,
                Status = BlockStatuses.Blocked,
                Inventory = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        calendar.LastSyncAtUtc = now;
        calendar.LastSyncError = null;
        calendar.SyncedEventCount = events.Count;

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "iCal sync for calendar {CalendarId} (listing {ListingId}): {Count} events",
            calendar.Id, calendar.ListingId, events.Count);

        return (events.Count, null);
    }

    /// <summary>
    /// Minimal iCal parser: extracts DTSTART, DTEND, SUMMARY from VEVENT blocks.
    /// Handles VALUE=DATE (all-day) and basic DATETIME formats.
    /// </summary>
    public static List<ICalEvent> ParseVEvents(string icsText)
    {
        var events = new List<ICalEvent>();
        var lines = icsText.Split('\n');

        ICalEvent? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r');

            if (line == "BEGIN:VEVENT")
            {
                current = new ICalEvent();
                continue;
            }

            if (line == "END:VEVENT")
            {
                if (current is not null && current.DtStart != default)
                {
                    if (current.DtEnd == default)
                        current.DtEnd = current.DtStart.AddDays(1);
                    events.Add(current);
                }
                current = null;
                continue;
            }

            if (current is null) continue;

            if (line.StartsWith("DTSTART"))
            {
                current.DtStart = ParseICalDate(line);
            }
            else if (line.StartsWith("DTEND"))
            {
                current.DtEnd = ParseICalDate(line);
            }
            else if (line.StartsWith("SUMMARY"))
            {
                var colonIdx = line.IndexOf(':');
                if (colonIdx >= 0)
                    current.Summary = line[(colonIdx + 1)..].Trim();
            }
        }

        return events;
    }

    private static DateTime ParseICalDate(string line)
    {
        var colonIdx = line.IndexOf(':');
        if (colonIdx < 0) return default;

        var value = line[(colonIdx + 1)..].Trim();

        if (value.Length == 8 && DateTime.TryParseExact(value, "yyyyMMdd",
                null, System.Globalization.DateTimeStyles.None, out var dateOnly))
            return dateOnly;

        if (value.Length >= 15 && DateTime.TryParseExact(
                value.TrimEnd('Z'), "yyyyMMddTHHmmss",
                null, System.Globalization.DateTimeStyles.AssumeUniversal, out var dateTime))
            return dateTime.Date;

        return default;
    }
}

public class ICalEvent
{
    public DateTime DtStart { get; set; }
    public DateTime DtEnd { get; set; }
    public string Summary { get; set; } = "";
}
