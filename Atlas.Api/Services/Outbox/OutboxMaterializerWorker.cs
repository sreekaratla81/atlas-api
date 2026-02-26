namespace Atlas.Api.Services.Outbox;

/// <summary>Polls OutboxMessage every 5 seconds; materializer creates AutomationSchedule and marks Published.</summary>
public sealed class OutboxMaterializerWorker : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxMaterializerWorker> _logger;

    public OutboxMaterializerWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxMaterializerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox materializer worker started (poll interval: {Interval}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var materializer = scope.ServiceProvider.GetRequiredService<IOutboxMaterializer>();
                await materializer.MaterializePendingBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox materializer iteration failed.");
            }

            await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Outbox materializer worker stopped.");
    }
}
