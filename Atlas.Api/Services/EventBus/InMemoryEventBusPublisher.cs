using System.Collections.Concurrent;

namespace Atlas.Api.Services.EventBus;

/// <summary>Captures published messages for tests. No-op publish.</summary>
public sealed class InMemoryEventBusPublisher : IEventBusPublisher
{
    public readonly ConcurrentQueue<CapturedMessage> Captured = new();

    public Task PublishAsync(string topic, string messageId, string? sessionId, IReadOnlyDictionary<string, object> applicationProperties, ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        Captured.Enqueue(new CapturedMessage(topic, messageId, sessionId, new Dictionary<string, object>(applicationProperties), body.ToArray()));
        return Task.CompletedTask;
    }

    public record CapturedMessage(string Topic, string MessageId, string? SessionId, IReadOnlyDictionary<string, object> ApplicationProperties, byte[] Body);
}
