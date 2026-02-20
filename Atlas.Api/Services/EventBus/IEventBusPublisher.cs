namespace Atlas.Api.Services.EventBus;

/// <summary>Publishes event envelopes to a topic. Used by outbox dispatcher; implement with Azure Service Bus or in-memory for tests.</summary>
public interface IEventBusPublisher
{
    Task PublishAsync(string topic, string messageId, string? sessionId, IReadOnlyDictionary<string, object> applicationProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default);
}
