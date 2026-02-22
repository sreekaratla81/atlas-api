using Atlas.Api.Events;
using Atlas.Api.Options;
using Atlas.Api.Services.Notifications;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;

namespace Atlas.Api.Services.Consumers;

/// <summary>Consumes booking.events / notifications; handles booking.confirmed, booking.cancelled; calls NotificationOrchestrator (idempotent).</summary>
public sealed class BookingEventsNotificationConsumer : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BookingEventsNotificationConsumer> _logger;
    private readonly AzureServiceBusOptions _options;

    public BookingEventsNotificationConsumer(
        IServiceScopeFactory scopeFactory,
        ILogger<BookingEventsNotificationConsumer> logger,
        IOptions<AzureServiceBusOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogInformation("Booking events notification consumer disabled: no Service Bus connection.");
            return;
        }

        await using var client = new ServiceBusClient(_options.ConnectionString);
        if (_options.EnableSessions)
        {
            var sessionOptions = new ServiceBusSessionProcessorOptions
            {
                MaxConcurrentSessions = _options.MaxConcurrentSessions,
                AutoCompleteMessages = false
            };
            await using var processor = client.CreateSessionProcessor(_options.TopicBookingEvents, _options.SubscriptionNotifications, sessionOptions);
            processor.ProcessMessageAsync += async args => await ProcessMessageAsync(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, stoppingToken).ConfigureAwait(false);
            processor.ProcessErrorAsync += args => { _logger.LogError(args.Exception, "Booking events consumer error: {Source}.", args.ErrorSource); return Task.CompletedTask; };
            _logger.LogInformation("Booking events notification consumer started (topic={Topic}, sub={Sub}, sessions).", _options.TopicBookingEvents, _options.SubscriptionNotifications);
            await processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);
        }
        else
        {
            await using var processor = client.CreateProcessor(_options.TopicBookingEvents, _options.SubscriptionNotifications, new ServiceBusProcessorOptions { AutoCompleteMessages = false });
            processor.ProcessMessageAsync += async args => await ProcessMessageAsync(args.Message, args.CompleteMessageAsync, args.AbandonMessageAsync, stoppingToken).ConfigureAwait(false);
            processor.ProcessErrorAsync += args => { _logger.LogError(args.Exception, "Booking events consumer error: {Source}.", args.ErrorSource); return Task.CompletedTask; };
            _logger.LogInformation("Booking events notification consumer started (topic={Topic}, sub={Sub}).", _options.TopicBookingEvents, _options.SubscriptionNotifications);
            await processor.StartProcessingAsync(stoppingToken).ConfigureAwait(false);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task ProcessMessageAsync(
        ServiceBusReceivedMessage message,
        Func<ServiceBusReceivedMessage, CancellationToken, Task> completeAsync,
        Func<ServiceBusReceivedMessage, IDictionary<string, object>?, CancellationToken, Task> abandonAsync,
        CancellationToken cancellationToken)
    {
        var tenantId = GetIntProp(message, "TenantId");
        var eventType = message.ApplicationProperties.TryGetValue("EventType", out var et) ? et?.ToString() ?? "" : "";
        var entityId = message.ApplicationProperties.TryGetValue("EntityId", out var eid) ? eid?.ToString() : null;
        var correlationId = message.ApplicationProperties.TryGetValue("CorrelationId", out var c) ? c?.ToString() : null;
        var eventId = message.ApplicationProperties.TryGetValue("IdempotencyKey", out var id) ? id?.ToString() : message.MessageId ?? Guid.NewGuid().ToString() ?? "";

        if (!EventTypes.IsBookingEvent(eventType))
        {
            await completeAsync(message, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (message.DeliveryCount > 5)
        {
            _logger.LogError("Booking event exceeded max delivery count ({DeliveryCount}): {EventType} tenant {TenantId} messageId {MessageId}. Dead-lettering.",
                message.DeliveryCount, eventType, tenantId, message.MessageId);
            await completeAsync(message, cancellationToken).ConfigureAwait(false);
            return;
        }

        var payloadJson = message.Body?.ToString() ?? "{}";
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
            await orchestrator.HandleEventAsync(tenantId, eventType, entityId, correlationId, eventId ?? "", payloadJson, cancellationToken).ConfigureAwait(false);
            await completeAsync(message, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Booking event processed: {EventType} tenant {TenantId} entity {EntityId}.", eventType, tenantId, entityId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Booking event processing failed (attempt {DeliveryCount}): {EventType} tenant {TenantId}.", message.DeliveryCount, eventType, tenantId);
            await abandonAsync(message, null, cancellationToken).ConfigureAwait(false);
        }
    }

    private static int GetIntProp(ServiceBusReceivedMessage message, string key)
    {
        if (!message.ApplicationProperties.TryGetValue(key, out var v) || v == null) return 0;
        if (v is int i) return i;
        if (v is long l) return (int)l;
        return int.TryParse(v.ToString(), out var n) ? n : 0;
    }
}
