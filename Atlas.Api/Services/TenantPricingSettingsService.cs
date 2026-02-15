using Atlas.Api.Data;
using Atlas.Api.DTOs;
using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Api.Services;

public interface ITenantPricingSettingsService
{
    Task<TenantPricingSetting> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<TenantPricingSetting> UpdateCurrentAsync(UpdateTenantPricingSettingsDto request, CancellationToken cancellationToken = default);
    void Invalidate(int tenantId);
}

public class TenantPricingSettingsService : ITenantPricingSettingsService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private readonly AppDbContext _dbContext;
    private readonly IMemoryCache _memoryCache;
    private readonly Services.Tenancy.ITenantContextAccessor _tenantContextAccessor;

    public TenantPricingSettingsService(AppDbContext dbContext, IMemoryCache memoryCache, Services.Tenancy.ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _memoryCache = memoryCache;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public async Task<TenantPricingSetting> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContextAccessor.TenantId ?? 1;
        var cacheKey = $"tenant-pricing:{tenantId}";
        if (_memoryCache.TryGetValue(cacheKey, out TenantPricingSetting? cached) && cached is not null)
        {
            return cached;
        }

        var settings = await _dbContext.TenantPricingSettings
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantPricingSetting
            {
                ConvenienceFeePercent = 3.00m,
                GlobalDiscountPercent = 0.00m,
                UpdatedAtUtc = DateTime.UtcNow
            };
            _dbContext.TenantPricingSettings.Add(settings);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _memoryCache.Set(cacheKey, settings, CacheTtl);
        return settings;
    }

    public async Task<TenantPricingSetting> UpdateCurrentAsync(UpdateTenantPricingSettingsDto request, CancellationToken cancellationToken = default)
    {
        var settings = await GetCurrentAsync(cancellationToken);
        settings.ConvenienceFeePercent = request.ConvenienceFeePercent;
        settings.GlobalDiscountPercent = request.GlobalDiscountPercent;
        settings.UpdatedBy = request.UpdatedBy;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        Invalidate(settings.TenantId);
        return settings;
    }

    public void Invalidate(int tenantId) => _memoryCache.Remove($"tenant-pricing:{tenantId}");
}
