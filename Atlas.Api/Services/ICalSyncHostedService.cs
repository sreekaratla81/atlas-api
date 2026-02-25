using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services;

/// <summary>
/// Background service that polls all active ListingExternalCalendar entries every 15 minutes,
/// downloads their iCal feeds, and upserts AvailabilityBlocks.
/// </summary>
public sealed class ICalSyncHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ICalSyncHostedService> _logger;

    public ICalSyncHostedService(
        IServiceScopeFactory scopeFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<ICalSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ICalSyncHostedService started. Poll interval: {Interval}", PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncAllCalendars(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unhandled error in iCal sync loop");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task SyncAllCalendars(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var calendars = await db.ListingExternalCalendars.IgnoreQueryFilters()
            .Where(c => c.IsActive)
            .ToListAsync(ct);

        if (calendars.Count == 0) return;

        _logger.LogInformation("iCal sync: processing {Count} active calendars", calendars.Count);

        var httpClient = _httpClientFactory.CreateClient("iCalSync");

        foreach (var calendar in calendars)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await ICalSyncHelper.FetchAndSync(db, httpClient, calendar, _logger, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "iCal sync failed for calendar {CalendarId}", calendar.Id);

                calendar.LastSyncAtUtc = DateTime.UtcNow;
                calendar.LastSyncError = $"Sync error: {ex.Message}"[..Math.Min(500, $"Sync error: {ex.Message}".Length)];
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
