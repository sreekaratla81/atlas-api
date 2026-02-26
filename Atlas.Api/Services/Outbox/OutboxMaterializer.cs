using System.Text.Json;
using Atlas.Api.Constants;
using Atlas.Api.Data;
using Atlas.Api.Events;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Api.Services.Outbox;

/// <summary>
/// Outbox Materializer: polls OutboxMessage (Pending, due), creates AutomationSchedule send jobs,
/// marks OutboxMessage Published. Does NOT send; the Sender polls AutomationSchedule.
/// </summary>
public sealed class OutboxMaterializer : IOutboxMaterializer
{
    private const int BatchSize = 20;
    private const int MaxAttempts = 5;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxMaterializer> _logger;

    public OutboxMaterializer(IServiceScopeFactory scopeFactory, ILogger<OutboxMaterializer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task MaterializePendingBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        List<OutboxMessage> claimed;

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var sql = $@"
SELECT o.* FROM OutboxMessage o WITH (UPDLOCK, READPAST)
WHERE o.Status = {{0}}
  AND (o.NextAttemptUtc IS NULL OR o.NextAttemptUtc <= {{1}})
ORDER BY o.CreatedAtUtc
OFFSET 0 ROWS FETCH NEXT {BatchSize} ROWS ONLY";

            claimed = await db.OutboxMessages
                .FromSqlRaw(sql, OutboxStatuses.Pending, now)
                .IgnoreQueryFilters()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (claimed.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            foreach (var row in claimed)
            {
                row.Status = OutboxStatuses.Processing;
                row.UpdatedAtUtc = now;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Outbox materializer claimed {Count} message(s).", claimed.Count);

        foreach (var row in claimed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var scope = _scopeFactory.CreateScope();
            await ProcessOneAsync(row, scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessOneAsync(OutboxMessage row, IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var db = scopedProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var entity = await db.OutboxMessages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == row.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null || entity.Status != OutboxStatuses.Processing)
        {
            _logger.LogWarning("Outbox message {Id} not found or not Processing.", row.Id);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            if (entity.EventType != EventTypes.BookingConfirmed)
            {
                _logger.LogDebug("Outbox message {Id}: EventType {EventType} not materialized, marking Published.", entity.Id, entity.EventType);
                entity.Status = OutboxStatuses.Published;
                entity.PublishedAtUtc = DateTime.UtcNow;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var payload = JsonSerializer.Deserialize<BookingConfirmedEvent>(entity.PayloadJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (payload == null)
            {
                _logger.LogWarning("Outbox message {Id}: invalid PayloadJson.", entity.Id);
                MarkFailed(entity, "Invalid PayloadJson");
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var schedule = await db.AutomationSchedules
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.BookingId == payload.BookingId && s.EventType == entity.EventType, cancellationToken)
                .ConfigureAwait(false);

            if (schedule == null)
            {
                schedule = new AutomationSchedule
                {
                    TenantId = entity.TenantId,
                    BookingId = payload.BookingId,
                    EventType = entity.EventType,
                    DueAtUtc = DateTime.UtcNow,
                    Status = ScheduleStatuses.Pending,
                    AttemptCount = 0
                };
                db.AutomationSchedules.Add(schedule);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Created AutomationSchedule for Booking {BookingId} EventType {EventType}.", payload.BookingId, entity.EventType);
            }

            entity.Status = OutboxStatuses.Published;
            entity.PublishedAtUtc = DateTime.UtcNow;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            entity.LastError = null;
            entity.NextAttemptUtc = null;

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Outbox message {Id} materialized and marked Published.", entity.Id);
        }
        catch (Exception ex)
        {
            entity.LastError = ex.Message;
            entity.AttemptCount += 1;
            entity.NextAttemptUtc = DateTime.UtcNow.Add(ExponentialBackoff(entity.AttemptCount));
            entity.UpdatedAtUtc = DateTime.UtcNow;

            if (entity.AttemptCount >= MaxAttempts)
            {
                entity.Status = OutboxStatuses.Failed;
                entity.NextAttemptUtc = null;
                _logger.LogError(ex, "Outbox message {Id} failed after {Attempts} attempts.", row.Id, entity.AttemptCount);
            }
            else
            {
                entity.Status = OutboxStatuses.Pending;
                _logger.LogWarning(ex, "Outbox message {Id} attempt {Attempt} failed.", row.Id, entity.AttemptCount);
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist outbox failure for {Id}.", row.Id);
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private void MarkFailed(OutboxMessage entity, string error)
    {
        entity.AttemptCount += 1;
        entity.LastError = error;
        entity.NextAttemptUtc = DateTime.UtcNow.Add(ExponentialBackoff(entity.AttemptCount));
        entity.UpdatedAtUtc = DateTime.UtcNow;
        entity.Status = entity.AttemptCount >= MaxAttempts ? OutboxStatuses.Failed : OutboxStatuses.Pending;
        if (entity.AttemptCount >= MaxAttempts) entity.NextAttemptUtc = null;
    }

    private static TimeSpan ExponentialBackoff(int attemptCount) =>
        TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attemptCount, 10)));
}
