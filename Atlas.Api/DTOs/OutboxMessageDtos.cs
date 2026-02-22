namespace Atlas.Api.DTOs;

/// <summary>Outbox message data for ops diagnostics.</summary>
public class OutboxMessageDto
{
    public Guid Id { get; set; }
    public int TenantId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    [Obsolete("Use Topic/EntityId")]
    public string? AggregateType { get; set; }
    [Obsolete("Use EntityId")]
    public string? AggregateId { get; set; }
}
