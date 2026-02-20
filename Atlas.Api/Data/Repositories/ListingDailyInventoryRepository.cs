using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Data.Repositories;

public class ListingDailyInventoryRepository : IListingDailyInventoryRepository
{
    private readonly AppDbContext _db;

    public ListingDailyInventoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ListingDailyInventory>> GetForListingInRangeAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        return await _db.ListingDailyInventories
            .AsNoTracking()
            .Where(i => i.ListingId == listingId && i.Date >= start && i.Date < end)
            .OrderBy(i => i.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ListingDailyInventory>> GetForListingsInRangeAsync(IEnumerable<int> listingIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        var ids = listingIds.ToList();
        if (ids.Count == 0) return Array.Empty<ListingDailyInventory>();

        var start = startDate.Date;
        var end = endDate.Date;
        return await _db.ListingDailyInventories
            .AsNoTracking()
            .Where(i => ids.Contains(i.ListingId) && i.Date >= start && i.Date < end)
            .ToListAsync(cancellationToken);
    }

    public async Task<ListingDailyInventory> UpsertAsync(ListingDailyInventory entity, CancellationToken cancellationToken = default)
    {
        entity.Date = entity.Date.Date;
        entity.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await _db.ListingDailyInventories
            .FirstOrDefaultAsync(i => i.ListingId == entity.ListingId && i.Date == entity.Date, cancellationToken);

        if (existing != null)
        {
            existing.RoomsAvailable = entity.RoomsAvailable;
            existing.Source = entity.Source;
            existing.Reason = entity.Reason;
            existing.UpdatedAtUtc = entity.UpdatedAtUtc;
            existing.UpdatedByUserId = entity.UpdatedByUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        _db.ListingDailyInventories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
