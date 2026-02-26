namespace Atlas.Api.Services.Outbox;

/// <summary>Polls OutboxMessage (Pending, due), creates AutomationSchedule send jobs, marks OutboxMessage Published.</summary>
public interface IOutboxMaterializer
{
    Task MaterializePendingBatchAsync(CancellationToken cancellationToken = default);
}
