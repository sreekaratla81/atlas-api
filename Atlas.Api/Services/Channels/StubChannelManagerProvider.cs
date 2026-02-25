namespace Atlas.Api.Services.Channels;

/// <summary>Stub channel manager for dev/test that logs and returns success.</summary>
public sealed class StubChannelManagerProvider : IChannelManagerProvider
{
    private readonly ILogger<StubChannelManagerProvider> _logger;

    public StubChannelManagerProvider(ILogger<StubChannelManagerProvider> logger) => _logger = logger;

    public string ProviderName => "stub";

    public Task<ChannelConnectionResult> TestConnectionAsync(string apiKey, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB-CHANNEL] TestConnection called with key={KeyPrefix}...", apiKey.Length > 4 ? apiKey[..4] : "****");
        return Task.FromResult(new ChannelConnectionResult { Connected = true, Message = "Stub: connected (dev mode)" });
    }

    public Task<ChannelSyncResult> PushRatesAsync(string apiKey, string externalPropertyId, List<RateUpdate> rates, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB-CHANNEL] PushRates: {Count} rates for property {PropertyId}", rates.Count, externalPropertyId);
        return Task.FromResult(new ChannelSyncResult { SyncedCount = rates.Count });
    }

    public Task<ChannelSyncResult> PushAvailabilityAsync(string apiKey, string externalPropertyId, List<AvailabilityUpdate> availability, CancellationToken ct = default)
    {
        _logger.LogInformation("[STUB-CHANNEL] PushAvailability: {Count} updates for property {PropertyId}", availability.Count, externalPropertyId);
        return Task.FromResult(new ChannelSyncResult { SyncedCount = availability.Count });
    }
}
