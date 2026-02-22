using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Options;
using Atlas.Api.Services.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services.Outbox;

/// <summary>Polls OutboxMessage (Pending, due), publishes to Service Bus via IEventBusPublisher, marks Published or Failed with backoff.</summary>
public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;
    private readonly AzureServiceBusOptions _options;
    private readonly int _maxAttempts;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcherHostedService> logger,
        IOptions<AzureServiceBusOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        _maxAttempts = _options.OutboxMaxAttempts;
        _pollInterval = TimeSpan.FromSeconds(_options.OutboxPollIntervalSeconds);
        _batchSize = _options.OutboxBatchSize;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogInformation("Outbox dispatcher disabled: Azure Service Bus connection string not configured.");
            return;
        }

        _logger.LogInformation("Outbox dispatcher started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox dispatcher iteration failed.");
            }

            await Task.Delay(_pollInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessPendingBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventBusPublisher>();

        var now = DateTime.UtcNow;
        var pending = await db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(o => o.Status == "Pending" && (o.NextAttemptUtc == null || o.NextAttemptUtc <= now))
            .OrderBy(o => o.NextAttemptUtc ?? o.CreatedAtUtc)
            .Take(_batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pending.Count == 0)
            return;

        foreach (var row in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneAsync(db, publisher, row, cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessOneAsync(AppDbContext db, IEventBusPublisher publisher, OutboxMessage row, CancellationToken cancellationToken)
    {
        var entity = await db.OutboxMessages.FindAsync(new object[] { row.Id }, cancellationToken).ConfigureAwait(false);
        if (entity == null || entity.Status != "Pending")
            return;

        entity.AttemptCount++;
        entity.LastError = null;

        try
        {
            var sessionId = _options.EnableSessions && !string.IsNullOrEmpty(entity.EntityId)
                ? $"{entity.TenantId}:{entity.EntityId}"
                : null;

            var props = new Dictionary<string, object>
            {
                ["TenantId"] = entity.TenantId,
                ["EventType"] = entity.EventType,
                ["EntityId"] = entity.EntityId ?? "",
                ["SchemaVersion"] = entity.SchemaVersion,
                ["CorrelationId"] = entity.CorrelationId ?? "",
                ["IdempotencyKey"] = entity.Id.ToString(),
            };

            var body = Encoding.UTF8.GetBytes(entity.PayloadJson);
            await publisher.PublishAsync(entity.Topic, entity.Id.ToString(), sessionId, props, body, cancellationToken).ConfigureAwait(false);

            entity.Status = "Published";
            entity.PublishedAtUtc = DateTime.UtcNow;
            entity.NextAttemptUtc = null;
            _logger.LogInformation("Outbox message {EventId} published to {Topic} (tenant {TenantId}, type {EventType}).", entity.Id, entity.Topic, entity.TenantId, entity.EventType);
        }
        catch (Exception ex)
        {
            entity.LastError = ex.Message;
            if (entity.AttemptCount >= _maxAttempts)
            {
                entity.Status = "Failed";
                entity.NextAttemptUtc = null;
                _logger.LogError(ex, "Outbox message {EventId} failed after {Attempts} attempts.", entity.Id, entity.AttemptCount);
            }
            else
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, entity.AttemptCount));
                entity.NextAttemptUtc = DateTime.UtcNow.Add(delay);
                _logger.LogWarning(ex, "Outbox message {EventId} attempt {Attempt} failed; next at {Next}.", entity.Id, entity.AttemptCount, entity.NextAttemptUtc);
            }
        }
    }
}
