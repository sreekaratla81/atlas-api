namespace Atlas.Api.Services.Scheduling;

/// <summary>Polls AutomationSchedule every 5 seconds; sender sends via providers and writes CommunicationLog.</summary>
public sealed class ScheduleSenderWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleSenderWorker> _logger;

    public ScheduleSenderWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduleSenderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Schedule sender worker started (poll interval: {Interval}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<IScheduleSender>();
                await sender.ProcessDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Schedule sender iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Schedule sender worker stopped.");
    }
}
