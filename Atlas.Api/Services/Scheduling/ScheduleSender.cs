using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Atlas.Api.Services.Communication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Services.Scheduling;

/// <summary>
/// Sender: polls AutomationSchedule (Pending and due), checks CommunicationLog idempotency,
/// sends via ICommunicationSender, inserts CommunicationLog, marks AutomationSchedule Completed.
/// </summary>
public sealed class ScheduleSender : IScheduleSender
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleSender> _logger;

    public ScheduleSender(IServiceScopeFactory scopeFactory, ILogger<ScheduleSender> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task ProcessDueSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        List<AutomationSchedule> due;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            due = await db.AutomationSchedules
                .IgnoreQueryFilters()
                .Where(s => s.Status == ScheduleStatuses.Pending && s.DueAtUtc <= now)
                .OrderBy(s => s.DueAtUtc)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (due.Count == 0) return;
        }

        _logger.LogInformation("Schedule sender processing {Count} due schedule(s).", due.Count);

        foreach (var schedule in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = _scopeFactory.CreateScope();
            await ProcessOneAsync(schedule, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(AutomationSchedule scheduleRow, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var db = scopedProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var schedule = await db.AutomationSchedules
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == scheduleRow.Id, cancellationToken)
            .ConfigureAwait(false);

        if (schedule == null || schedule.Status != ScheduleStatuses.Pending)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (schedule.EventType != EventTypes.BookingConfirmed)
        {
            _logger.LogDebug("Schedule {Id}: EventType {EventType} not handled by sender.", schedule.Id, schedule.EventType);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var booking = await db.Bookings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(b => b.Guest)
            .Include(b => b.Listing)
            .FirstOrDefaultAsync(b => b.Id == schedule.BookingId, cancellationToken)
            .ConfigureAwait(false);

        if (booking == null)
        {
            schedule.Status = ScheduleStatuses.Failed;
            schedule.LastError = $"Booking {schedule.BookingId} not found.";
            schedule.AttemptCount++;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var aggregateId = schedule.BookingId.ToString();
        var correlationId = Guid.NewGuid().ToString();
        var payload = new Events.BookingConfirmedEvent
        {
            BookingId = schedule.BookingId,
            GuestId = booking.GuestId,
            ListingId = booking.ListingId,
            BookingStatus = booking.BookingStatus,
            CheckinDate = booking.CheckinDate,
            CheckoutDate = booking.CheckoutDate,
            GuestPhone = booking.Guest?.Phone,
            GuestEmail = booking.Guest?.Email,
            OccurredAtUtc = DateTime.UtcNow
        };

        var channels = new[] { "Email", "SMS", "WhatsApp" };
        var allSent = true;
        foreach (var ch in channels)
        {
            var key = $"{EventTypes.BookingConfirmed}:{aggregateId}:{ch}";
            var exists = await db.CommunicationLogs
                .IgnoreQueryFilters()
                .AnyAsync(c => c.TenantId == schedule.TenantId && c.IdempotencyKey == key && c.Status == "Sent", cancellationToken)
                .ConfigureAwait(false);
            if (!exists)
            {
                allSent = false;
                break;
            }
        }

        if (allSent)
        {
            schedule.Status = ScheduleStatuses.Completed;
            schedule.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Schedule {Id}: all channels already sent, marked Completed.", schedule.Id);
            return;
        }

        try
        {
            var sender = scopedProvider.GetRequiredService<ICommunicationSender>();
            await sender.SendBookingConfirmedAsync(aggregateId, correlationId, schedule.TenantId, payload, cancellationToken).ConfigureAwait(false);

            schedule.Status = ScheduleStatuses.Completed;
            schedule.CompletedAtUtc = DateTime.UtcNow;
            schedule.LastError = null;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Schedule {Id} completed (Booking {BookingId}).", schedule.Id, schedule.BookingId);
        }
        catch (Exception ex)
        {
            schedule.AttemptCount++;
            schedule.LastError = ex.Message;

            if (schedule.AttemptCount >= MaxAttempts)
            {
                schedule.Status = ScheduleStatuses.Failed;
                _logger.LogError(ex, "Schedule {Id} failed after {Attempts} attempts.", schedule.Id, schedule.AttemptCount);
            }
            else
            {
                _logger.LogWarning(ex, "Schedule {Id} attempt {Attempt} failed.", schedule.Id, schedule.AttemptCount);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist schedule failure for {Id}.", schedule.Id);
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
