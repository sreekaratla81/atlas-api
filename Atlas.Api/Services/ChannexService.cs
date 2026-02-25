using System.Text.Json;

namespace Atlas.Api.Services;

public interface IChannexService
{
    Task<ChannexConnectionResult> TestConnectionAsync(string apiKey, CancellationToken ct = default);
    Task<ChannexSyncResult> PushRatesAsync(int propertyId, string apiKey, string channexPropertyId, List<ChannexRateUpdate> rates, CancellationToken ct = default);
    Task<ChannexSyncResult> PushAvailabilityAsync(int propertyId, string apiKey, string channexPropertyId, List<ChannexAvailabilityUpdate> availability, CancellationToken ct = default);
    Task<List<ChannexProperty>> ListPropertiesAsync(string apiKey, CancellationToken ct = default);
}

public class ChannexService : IChannexService
{
    private readonly HttpClient _http;
    private readonly ILogger<ChannexService> _log;
    private const string BaseUrl = "https://staging.channex.io/api/v1";

    public ChannexService(HttpClient http, ILogger<ChannexService> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<ChannexConnectionResult> TestConnectionAsync(string apiKey, CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/properties");
            req.Headers.Add("user-api-key", apiKey);
            var res = await _http.SendAsync(req, ct);
            return new ChannexConnectionResult
            {
                Connected = res.IsSuccessStatusCode,
                Message = res.IsSuccessStatusCode ? "Connected successfully" : $"Connection failed: {res.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Channex connection test failed");
            return new ChannexConnectionResult { Connected = false, Message = $"Connection error: {ex.Message}" };
        }
    }

    public async Task<List<ChannexProperty>> ListPropertiesAsync(string apiKey, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/properties");
        req.Headers.Add("user-api-key", apiKey);
        var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var list = new List<ChannexProperty>();

        if (doc.RootElement.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in dataArr.EnumerateArray())
            {
                var attrs = item.GetProperty("attributes");
                list.Add(new ChannexProperty
                {
                    Id = item.GetProperty("id").GetString() ?? "",
                    Title = attrs.GetProperty("title").GetString() ?? "",
                    Currency = attrs.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "INR" : "INR"
                });
            }
        }

        return list;
    }

    public async Task<ChannexSyncResult> PushRatesAsync(int propertyId, string apiKey, string channexPropertyId, List<ChannexRateUpdate> rates, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var synced = 0;

        foreach (var rate in rates)
        {
            try
            {
                var payload = new
                {
                    values = new[]
                    {
                        new
                        {
                            property_id = channexPropertyId,
                            room_type_id = rate.RoomTypeId,
                            rate_plan_id = rate.RatePlanId,
                            date_from = rate.DateFrom.ToString("yyyy-MM-dd"),
                            date_to = rate.DateTo.ToString("yyyy-MM-dd"),
                            rate = rate.Rate
                        }
                    }
                };

                var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/channel_rate_plans/bulk");
                req.Headers.Add("user-api-key", apiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                    synced++;
                else
                    errors.Add($"Rate push failed for {rate.DateFrom:yyyy-MM-dd}: {res.StatusCode}");
            }
            catch (Exception ex)
            {
                errors.Add($"Rate push error for {rate.DateFrom:yyyy-MM-dd}: {ex.Message}");
            }
        }

        return new ChannexSyncResult { SyncedCount = synced, Errors = errors };
    }

    public async Task<ChannexSyncResult> PushAvailabilityAsync(int propertyId, string apiKey, string channexPropertyId, List<ChannexAvailabilityUpdate> availability, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var synced = 0;

        foreach (var avail in availability)
        {
            try
            {
                var payload = new
                {
                    values = new[]
                    {
                        new
                        {
                            property_id = channexPropertyId,
                            room_type_id = avail.RoomTypeId,
                            date_from = avail.DateFrom.ToString("yyyy-MM-dd"),
                            date_to = avail.DateTo.ToString("yyyy-MM-dd"),
                            availability = avail.Available
                        }
                    }
                };

                var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/availability");
                req.Headers.Add("user-api-key", apiKey);
                req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                    synced++;
                else
                    errors.Add($"Availability push failed for {avail.DateFrom:yyyy-MM-dd}: {res.StatusCode}");
            }
            catch (Exception ex)
            {
                errors.Add($"Availability push error for {avail.DateFrom:yyyy-MM-dd}: {ex.Message}");
            }
        }

        return new ChannexSyncResult { SyncedCount = synced, Errors = errors };
    }
}

public class ChannexConnectionResult
{
    public bool Connected { get; set; }
    public string Message { get; set; } = "";
}

public class ChannexProperty
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Currency { get; set; } = "INR";
}

public class ChannexRateUpdate
{
    public string RoomTypeId { get; set; } = "";
    public string RatePlanId { get; set; } = "";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public decimal Rate { get; set; }
}

public class ChannexAvailabilityUpdate
{
    public string RoomTypeId { get; set; } = "";
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int Available { get; set; }
}

public class ChannexSyncResult
{
    public int SyncedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool Success => Errors.Count == 0;
}
