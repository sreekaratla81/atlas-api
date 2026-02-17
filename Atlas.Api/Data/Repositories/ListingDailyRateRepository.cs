using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Data.Repositories;

public class ListingDailyRateRepository : IListingDailyRateRepository
{
    private readonly AppDbContext _db;

    public ListingDailyRateRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ListingDailyRate>> GetForListingInRangeAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        return await _db.ListingDailyRates
            .AsNoTracking()
            .Where(r => r.ListingId == listingId && r.Date >= start && r.Date < end)
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ListingDailyRate>> GetForListingsInRangeAsync(IEnumerable<int> listingIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var ids = listingIds.ToList();
        if (ids.Count == 0) return Array.Empty<ListingDailyRate>();

        var start = startDate.Date;
        var end = endDate.Date;
        return await _db.ListingDailyRates
            .AsNoTracking()
            .Where(r => ids.Contains(r.ListingId) && r.Date >= start && r.Date < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<ListingDailyRate> UpsertAsync(ListingDailyRate entity, CancellationToken cancellationToken = default)
    {
        entity.Date = entity.Date.Date;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await _db.ListingDailyRates
            .FirstOrDefaultAsync(r => r.ListingId == entity.ListingId && r.Date == entity.Date, cancellationToken);

        if (existing != null)
        {
            existing.NightlyRate = entity.NightlyRate;
            existing.Currency = entity.Currency;
            existing.Source = entity.Source;
            existing.Reason = entity.Reason;
            existing.UpdatedAtUtc = entity.UpdatedAtUtc;
            existing.UpdatedByUserId = entity.UpdatedByUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        _db.ListingDailyRates.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
