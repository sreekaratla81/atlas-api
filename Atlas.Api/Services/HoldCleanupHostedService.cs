using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services;

/// <summary>
/// Periodically expires abandoned Hold bookings and their availability blocks.
/// Hold bookings older than <see cref="HoldExpiryMinutes"/> that are still in
/// "Hold" status are moved to "Expired" so they no longer occupy availability.
/// </summary>
public sealed class HoldCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HoldCleanupHostedService> _logger;
    private const int HoldExpiryMinutes = 10;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    public HoldCleanupHostedService(IServiceScopeFactory scopeFactory, ILogger<HoldCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hold cleanup service started (expiry={ExpiryMinutes}m, poll={PollSeconds}s).",
            HoldExpiryMinutes, PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredHoldsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hold cleanup iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task CleanupExpiredHoldsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTime.UtcNow.AddMinutes(-HoldExpiryMinutes);

        var expiredBlocks = await db.AvailabilityBlocks
            .IgnoreQueryFilters()
            .Where(b => b.Status == "Hold" && b.CreatedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var block in expiredBlocks)
        {
            block.Status = "Expired";
            block.UpdatedAtUtc = DateTime.UtcNow;
        }

        var expiredBookingIds = expiredBlocks
            .Where(b => b.BookingId.HasValue)
            .Select(b => b.BookingId!.Value)
            .Distinct()
            .ToList();

        if (expiredBookingIds.Count > 0)
        {
            var holdBookings = await db.Bookings
                .IgnoreQueryFilters()
                .Where(b => expiredBookingIds.Contains(b.Id) && b.BookingStatus == "Hold")
                .ToListAsync(cancellationToken);

            foreach (var booking in holdBookings)
            {
                booking.BookingStatus = "Expired";
            }
        }

        if (expiredBlocks.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Expired {BlockCount} hold blocks and {BookingCount} hold bookings older than {Minutes}m.",
                expiredBlocks.Count, expiredBookingIds.Count, HoldExpiryMinutes);
        }
    }
}
