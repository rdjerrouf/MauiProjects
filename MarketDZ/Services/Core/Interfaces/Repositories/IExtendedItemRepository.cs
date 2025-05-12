using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Services.Core.Interfaces.Repositories;

/// <summary>
/// Extended item repository interface with additional query methods
/// This interface extends IItemRepository with location-based queries and advanced operations
/// </summary>
public interface IExtendedItemRepository : IItemRepository
{
    /// <summary>
    /// Get items near a location
    /// </summary>
    Task<IEnumerable<Item>> GetNearbyAsync(double latitude, double longitude, double radiusKm);

    /// <summary>
    /// Get items using query parameters
    /// </summary>
    Task<PaginatedResult<Item>> GetPaginatedAsync(ItemQueryParameters parameters);

    /// <summary>
    /// Get items by user ID with string parameter (overload)
    /// </summary>
    new Task<IEnumerable<Item>> GetByUserIdAsync(string userId);
}
