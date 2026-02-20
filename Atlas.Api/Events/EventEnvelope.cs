namespace Atlas.Api.Events;

/// <summary>Standard envelope for events published to Service Bus. Multi-tenant safe; use application properties for routing.</summary>
public sealed class EventEnvelope<T>
{
    public string EventId { get; set; } = Guid.NewGuid().ToString();
    public int TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
    public string? CorrelationId { get; set; }
    public string? EntityId { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public T Payload { get; set; } = default!;
}
