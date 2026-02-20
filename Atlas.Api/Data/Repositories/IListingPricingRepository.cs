using Atlas.Api.Models;

namespace Atlas.Api.Data.Repositories;

public interface IListingPricingRepository
{
    Task<ListingPricing?> GetByListingIdAsync(int listingId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ListingPricing>> GetByListingIdsAsync(IEnumerable<int> listingIds, CancellationToken cancellationToken = default);
    Task<ListingPricing> UpsertAsync(ListingPricing entity, CancellationToken cancellationToken = default);
}
