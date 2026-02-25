namespace Atlas.Api.Services.Channels;

public interface IChannelManagerProvider
{
    string ProviderName { get; }
    Task<ChannelConnectionResult> TestConnectionAsync(string apiKey, CancellationToken ct = default);
    Task<ChannelSyncResult> PushRatesAsync(string apiKey, string externalPropertyId, List<RateUpdate> rates, CancellationToken ct = default);
    Task<ChannelSyncResult> PushAvailabilityAsync(string apiKey, string externalPropertyId, List<AvailabilityUpdate> availability, CancellationToken ct = default);
}

public class ChannelConnectionResult
{
    public bool Connected { get; set; }
    public string Message { get; set; } = "";
}

public class ChannelSyncResult
{
    public int SyncedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}

public class RateUpdate
{
    public string RoomTypeId { get; set; } = "";
    public string RatePlanId { get; set; } = "";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal Rate { get; set; }
}

public class AvailabilityUpdate
{
    public string RoomTypeId { get; set; } = "";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int Available { get; set; }
}
