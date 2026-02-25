namespace Atlas.Api.Services.Channels;

/// <summary>Adapts the existing IChannexService to the IChannelManagerProvider abstraction.</summary>
public sealed class ChannexAdapter : IChannelManagerProvider
{
    private readonly IChannexService _channex;

    public ChannexAdapter(IChannexService channex) => _channex = channex;

    public string ProviderName => "channex";

    public async Task<ChannelConnectionResult> TestConnectionAsync(string apiKey, CancellationToken ct = default)
    {
        var r = await _channex.TestConnectionAsync(apiKey, ct);
        return new ChannelConnectionResult { Connected = r.Connected, Message = r.Message };
    }

    public async Task<ChannelSyncResult> PushRatesAsync(string apiKey, string externalPropertyId, List<RateUpdate> rates, CancellationToken ct = default)
    {
        var channexRates = rates.Select(r => new ChannexRateUpdate
        {
            RoomTypeId = r.RoomTypeId,
            RatePlanId = r.RatePlanId,
            DateFrom = r.DateFrom,
            DateTo = r.DateTo,
            Rate = r.Rate
        }).ToList();

        var r2 = await _channex.PushRatesAsync(0, apiKey, externalPropertyId, channexRates, ct);
        return new ChannelSyncResult { SyncedCount = r2.SyncedCount, Errors = r2.Errors };
    }

    public async Task<ChannelSyncResult> PushAvailabilityAsync(string apiKey, string externalPropertyId, List<AvailabilityUpdate> availability, CancellationToken ct = default)
    {
        var channexAvail = availability.Select(a => new ChannexAvailabilityUpdate
        {
            RoomTypeId = a.RoomTypeId,
            DateFrom = a.DateFrom,
            DateTo = a.DateTo,
            Available = a.Available
        }).ToList();

        var r = await _channex.PushAvailabilityAsync(0, apiKey, externalPropertyId, channexAvail, ct);
        return new ChannelSyncResult { SyncedCount = r.SyncedCount, Errors = r.Errors };
    }
}
