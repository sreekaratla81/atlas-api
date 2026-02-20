using Atlas.Api.Models;

namespace Atlas.Api.Data.Repositories;

public interface IListingDailyRateRepository
{
    Task<IReadOnlyList<ListingDailyRate>> GetForListingInRangeAsync(int listingId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ListingDailyRate>> GetForListingsInRangeAsync(IEnumerable<int> listingIds, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<ListingDailyRate> UpsertAsync(ListingDailyRate entity, CancellationToken cancellationToken = default);
}
