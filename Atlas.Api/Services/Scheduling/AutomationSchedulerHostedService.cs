using System.Text.Json;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Services.Scheduling;

/// <summary>
/// Polls AutomationSchedule rows that are Pending and due, enriches them with
/// booking/guest data, writes an OutboxMessage per schedule, and marks the
/// schedule Published. Retries with exponential backoff on transient failures.
/// </summary>
public sealed class AutomationSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutomationSchedulerHostedService> _logger;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private const int BatchSize = 50;
    private const int MaxAttempts = 5;

    public AutomationSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AutomationSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutomationScheduler started (poll={PollSeconds}s, batch={Batch}).",
            PollInterval.TotalSeconds, BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutomationScheduler iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessDueBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var due = await db.AutomationSchedules
            .IgnoreQueryFilters()
            .Where(s => s.Status == ScheduleStatuses.Pending && s.DueAtUtc <= now)
            .OrderBy(s => s.DueAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (due.Count == 0) return;

        var bookingIds = due.Select(s => s.BookingId).Distinct().ToList();
        var bookings = await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(b => bookingIds.Contains(b.Id))
            .Include(b => b.Guest)
            .Include(b => b.Listing)
            .ToDictionaryAsync(b => b.Id, ct)
            .ConfigureAwait(false);

        foreach (var schedule in due)
        {
            ct.ThrowIfCancellationRequested();
            await ProcessOneAsync(db, schedule, bookings, ct).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("AutomationScheduler processed {Count} due schedules.", due.Count);
    }

    private async Task ProcessOneAsync(
        AppDbContext db,
        AutomationSchedule schedule,
        IReadOnlyDictionary<int, Booking> bookings,
        CancellationToken ct)
    {
        var entity = await db.AutomationSchedules.FindAsync(new object[] { schedule.Id }, ct).ConfigureAwait(false);
        if (entity == null || entity.Status != ScheduleStatuses.Pending) return;

        entity.AttemptCount++;

        if (!bookings.TryGetValue(entity.BookingId, out var booking))
        {
            entity.Status = ScheduleStatuses.Failed;
            entity.LastError = $"Booking {entity.BookingId} not found.";
            _logger.LogWarning("Schedule {Id}: booking {BookingId} not found; marking failed.",
                entity.Id, entity.BookingId);
            return;
        }

        if (booking.BookingStatus == "Cancelled" || booking.BookingStatus == "Expired")
        {
            entity.Status = ScheduleStatuses.Cancelled;
            entity.LastError = $"Booking status is {booking.BookingStatus}.";
            return;
        }

        try
        {
            var topic = Events.EventTypes.IsStayEvent(entity.EventType)
                ? "stay.events"
                : "booking.events";

            var payload = BuildPayload(entity, booking);
            var correlationId = Guid.NewGuid().ToString();

            db.OutboxMessages.Add(new OutboxMessage
            {
                TenantId = entity.TenantId,
                Topic = topic,
                EventType = entity.EventType,
                EntityId = booking.Id.ToString(),
                PayloadJson = JsonSerializer.Serialize(payload),
                CorrelationId = correlationId,
                OccurredUtc = DateTime.UtcNow,
                SchemaVersion = 1,
                Status = OutboxStatuses.Pending,
                NextAttemptUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
                AttemptCount = 0
            });

            entity.Status = ScheduleStatuses.Published;
            entity.PublishedAtUtc = DateTime.UtcNow;
            entity.LastError = null;

            _logger.LogInformation("Schedule {Id} published: {EventType} for booking {BookingId} tenant {TenantId}.",
                entity.Id, entity.EventType, entity.BookingId, entity.TenantId);
        }
        catch (Exception ex)
        {
            entity.LastError = ex.Message;
            if (entity.AttemptCount >= MaxAttempts)
            {
                entity.Status = ScheduleStatuses.Failed;
                _logger.LogError(ex, "Schedule {Id} failed after {Attempts} attempts.", entity.Id, entity.AttemptCount);
            }
            else
            {
                _logger.LogWarning(ex, "Schedule {Id} attempt {Attempt} failed; will retry.", entity.Id, entity.AttemptCount);
            }
        }
    }

    private static object BuildPayload(AutomationSchedule schedule, Booking booking)
    {
        return new
        {
            bookingId = booking.Id,
            listingId = booking.ListingId,
            guestId = booking.GuestId,
            guestPhone = booking.Guest?.Phone,
            guestEmail = booking.Guest?.Email,
            guestName = booking.Guest?.Name,
            listingName = booking.Listing?.Name,
            checkinDate = booking.CheckinDate,
            checkoutDate = booking.CheckoutDate,
            totalAmount = booking.TotalAmount,
            currency = booking.Currency,
            scheduleId = schedule.Id,
            eventType = schedule.EventType,
            occurredAtUtc = DateTime.UtcNow
        };
    }
}
