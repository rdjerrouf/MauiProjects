using MarketDZ.Models;
using MarketDZ.Models.Filters;
using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Infrastructure.Common;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for item-related operations
    /// </summary>
    public interface IItemRepository : IRepository<Item>
    {

        // Specialized filtering and search operations
        Task<IEnumerable<Item>> GetByUserIdAsync(string userId);
        Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters filter);
        Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria);
        Task<IEnumerable<Item>> GetByCategoryAsync(string category, FilterParameters? additionalFilters = null);
        Task<IEnumerable<Item>> GetByStateAsync(AlState state, FilterParameters? additionalFilters = null);
        Task<IEnumerable<Item>> SearchByTextAsync(string searchText);

        // Pagination
        Task<PaginatedResult<Item>> GetPaginatedAsync(FilterParameters filter);
        Task<PaginatedResult<Item>> GetPaginatedByCriteriaAsync(FilterCriteria criteria);

        // Statistics and status
        Task<bool> IncrementViewCountAsync(string itemId);
        Task<bool> IncrementInquiryCountAsync(string itemId);
        Task<ItemStatistics?> GetStatisticsAsync(string itemId);
        Task<bool> UpdateStatusAsync(string itemId, ItemStatus status);
        Task<bool> IsAvailableAsync(string itemId);
    }
}