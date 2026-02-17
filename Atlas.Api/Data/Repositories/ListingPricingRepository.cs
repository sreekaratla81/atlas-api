using Atlas.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Api.Data.Repositories;

public class ListingPricingRepository : IListingPricingRepository
{
    private readonly AppDbContext _db;

    public ListingPricingRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ListingPricing?> GetByListingIdAsync(int listingId, CancellationToken cancellationToken = default)
    {
        return await _db.ListingPricings
            .AsNoTracking()
            .Include(p => p.Listing)
            .FirstOrDefaultAsync(p => p.ListingId == listingId, cancellationToken);
    }

    public async Task<IReadOnlyList<ListingPricing>> GetByListingIdsAsync(IEnumerable<int> listingIds, CancellationToken cancellationToken = default)
    {
        var ids = listingIds.ToList();
        if (ids.Count == 0) return Array.Empty<ListingPricing>();

        return await _db.ListingPricings
            .AsNoTracking()
            .Where(p => ids.Contains(p.ListingId))
            .ToListAsync(cancellationToken);
    }

    public async Task<ListingPricing> UpsertAsync(ListingPricing entity, CancellationToken cancellationToken = default)
    {
        entity.UpdatedAtUtc = DateTime.UtcNow;
        var existing = await _db.ListingPricings.FindAsync(new object[] { entity.ListingId }, cancellationToken);
        if (existing != null)
        {
            existing.BaseNightlyRate = entity.BaseNightlyRate;
            existing.WeekendNightlyRate = entity.WeekendNightlyRate;
            existing.Currency = entity.Currency;
            existing.UpdatedAtUtc = entity.UpdatedAtUtc;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        _db.ListingPricings.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
