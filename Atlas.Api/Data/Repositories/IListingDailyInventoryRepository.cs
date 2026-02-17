using Atlas.Api.Models;

namespace Atlas.Api.Data.Repositories;

public interface IListingDailyInventoryRepository
{
    Task<IReadOnlyList<ListingDailyInventory>> GetForListingInRangeAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ListingDailyInventory>> GetForListingsInRangeAsync(IEnumerable<int> listingIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<ListingDailyInventory> UpsertAsync(ListingDailyInventory entity, CancellationToken cancellationToken = default);
}
