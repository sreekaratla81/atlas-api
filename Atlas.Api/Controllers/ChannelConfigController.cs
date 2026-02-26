using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Atlas.Api.Data;
using Atlas.Api.Models;
using Atlas.Api.Services;
using Atlas.Api.Services.Channels;

namespace Atlas.Api.Controllers;

[ApiController]
[Authorize(Roles = "platform-admin")]
[Route("api/channel-configs")]
[Produces("application/json")]
public class ChannelConfigController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IChannelManagerProvider _channelManager;

    public ChannelConfigController(AppDbContext db, IChannelManagerProvider channelManager)
    {
        _db = db;
        _channelManager = channelManager;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var configs = await _db.ChannelConfigs
            .AsNoTracking()
            .Select(c => new
            {
                c.Id, c.PropertyId, c.Provider, c.IsConnected,
                c.ExternalPropertyId, c.LastSyncAt, c.LastSyncError
            })
            .ToListAsync(ct);
        return Ok(configs);
    }

    [HttpGet("{propertyId:int}")]
    public async Task<IActionResult> GetByProperty(int propertyId, CancellationToken ct)
    {
        var config = await _db.ChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct);

        if (config is null)
            return Ok(new { connected = false, provider = "channex" });

        return Ok(new
        {
            config.Id, config.PropertyId, config.Provider,
            connected = config.IsConnected, config.ExternalPropertyId,
            config.LastSyncAt, config.LastSyncError, config.ApiKey
        });
    }

    [HttpPost("{propertyId:int}/connect")]
    public async Task<IActionResult> Connect(int propertyId, [FromBody] ConnectDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.ApiKey))
            return BadRequest(new { error = "API key is required." });

        var result = await _channelManager.TestConnectionAsync(dto.ApiKey, ct);

        var config = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct);

        if (config is null)
        {
            config = new ChannelConfig
            {
                PropertyId = propertyId,
                Provider = _channelManager.ProviderName,
                ApiKey = dto.ApiKey,
                ExternalPropertyId = dto.ExternalPropertyId,
                IsConnected = result.Connected,
                LastSyncError = result.Connected ? null : result.Message
            };
            _db.ChannelConfigs.Add(config);
        }
        else
        {
            config.ApiKey = dto.ApiKey;
            config.ExternalPropertyId = dto.ExternalPropertyId;
            config.IsConnected = result.Connected;
            config.LastSyncError = result.Connected ? null : result.Message;
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new { connected = result.Connected, message = result.Message });
    }

    [HttpPost("{propertyId:int}/disconnect")]
    public async Task<IActionResult> Disconnect(int propertyId, CancellationToken ct)
    {
        var config = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct);

        if (config is not null)
        {
            config.IsConnected = false;
            config.ApiKey = null;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { connected = false });
    }

    [HttpPost("{propertyId:int}/sync-rates")]
    public async Task<IActionResult> SyncRates(int propertyId, [FromBody] SyncRatesDto dto, CancellationToken ct)
    {
        var config = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct);

        if (config is null || !config.IsConnected || string.IsNullOrEmpty(config.ApiKey))
            return BadRequest(new { error = "Channel not connected for this property." });

        var rates = dto.Rates.Select(r => new RateUpdate
        {
            RoomTypeId = r.RoomTypeId,
            RatePlanId = r.RatePlanId,
            DateFrom = r.DateFrom,
            DateTo = r.DateTo,
            Rate = r.Rate
        }).ToList();

        var result = await _channelManager.PushRatesAsync(config.ApiKey, config.ExternalPropertyId ?? "", rates, ct);

        config.LastSyncAt = DateTime.UtcNow;
        config.LastSyncError = result.Success ? null : string.Join("; ", result.Errors.Take(3));
        await _db.SaveChangesAsync(ct);

        return Ok(new { result.SyncedCount, result.Errors, result.Success });
    }

    [HttpPost("{propertyId:int}/sync-availability")]
    public async Task<IActionResult> SyncAvailability(int propertyId, [FromBody] SyncAvailabilityDto dto, CancellationToken ct)
    {
        var config = await _db.ChannelConfigs
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId, ct);

        if (config is null || !config.IsConnected || string.IsNullOrEmpty(config.ApiKey))
            return BadRequest(new { error = "Channel not connected for this property." });

        var avail = dto.Availability.Select(a => new AvailabilityUpdate
        {
            RoomTypeId = a.RoomTypeId,
            DateFrom = a.DateFrom,
            DateTo = a.DateTo,
            Available = a.Available
        }).ToList();

        var result = await _channelManager.PushAvailabilityAsync(config.ApiKey, config.ExternalPropertyId ?? "", avail, ct);

        config.LastSyncAt = DateTime.UtcNow;
        config.LastSyncError = result.Success ? null : string.Join("; ", result.Errors.Take(3));
        await _db.SaveChangesAsync(ct);

        return Ok(new { result.SyncedCount, result.Errors, result.Success });
    }
}

public class ConnectDto
{
    public string ApiKey { get; set; } = "";
    public string? ExternalPropertyId { get; set; }
}

public class SyncRatesDto
{
    public List<RateItem> Rates { get; set; } = new();

    public class RateItem
    {
        public string RoomTypeId { get; set; } = "";
        public string RatePlanId { get; set; } = "";
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public decimal Rate { get; set; }
    }
}

public class SyncAvailabilityDto
{
    public List<AvailItem> Availability { get; set; } = new();

    public class AvailItem
    {
        public string RoomTypeId { get; set; } = "";
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public int Available { get; set; }
    }
}
