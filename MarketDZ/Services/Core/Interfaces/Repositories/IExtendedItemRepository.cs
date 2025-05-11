using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Models.Core.Entities;


namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    public interface IExtendedItemRepository
    {
        Task<Item?> GetByIdAsync(string itemId);
        Task<IEnumerable<Item>> GetAllAsync(int skip = 0, int take = 50);
        Task<int> CreateAsync(Item item);
        Task<bool> UpdateAsync(Item item);
        Task<bool> DeleteAsync(string itemId);
        Task<IEnumerable<Item>> GetByUserIdAsync(int userId);
        Task<IEnumerable<Item>> GetByCategoryAsync(string category);
        Task<IEnumerable<Item>> SearchByTextAsync(string searchText);
        Task<IEnumerable<Item>> GetNearbyAsync(double latitude, double longitude, double radiusKm);
        Task<PaginatedResult<Item>> GetPaginatedAsync(ItemQueryParameters parameters);
        Task<object> GetPaginatedAsync(FilterParameters filter);
        Task<object> GetPaginatedByCriteriaAsync(FilterCriteria criteria);
        Task<bool> IncrementViewCountAsync(string itemId);
        Task<bool> IncrementInquiryCountAsync(string itemId);
        Task<object> GetStatisticsAsync(string itemId);
        Task<bool> UpdateStatusAsync(string itemId, ItemStatus status);
        Task<object> IsAvailableAsync(string itemId);
        // Other item-related methods
    }
}
