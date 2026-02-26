namespace Atlas.Api.Services.Scheduling;

/// <summary>Polls AutomationSchedule (Pending, due), sends via providers, writes CommunicationLog, marks Completed.</summary>
public interface IScheduleSender
{
    Task ProcessDueSchedulesAsync(CancellationToken cancellationToken = default);
}
