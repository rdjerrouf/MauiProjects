using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for item-related operations
    /// </summary>
    public interface IItemRepository : IRepository<Item>
    {
        /// <summary>
        /// Gets items by user ID
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>Collection of items</returns>
        Task<IEnumerable<Item>> GetByUserIdAsync(string userId);

        /// <summary>
        /// Gets items filtered by parameters
        /// </summary>
        /// <param name="filter">Filter parameters</param>
        /// <returns>Collection of filtered items</returns>
        Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters filter);

        /// <summary>
        /// Gets items filtered by criteria
        /// </summary>
        /// <param name="criteria">Filter criteria</param>
        /// <returns>Collection of filtered items</returns>
        Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria);

        /// <summary>
        /// Gets items by category
        /// </summary>
        /// <param name="category">Category name</param>
        /// <param name="additionalFilters">Optional additional filters</param>
        /// <returns>Collection of items</returns>
        Task<IEnumerable<Item>> GetByCategoryAsync(string category, FilterParameters additionalFilters = null);

        /// <summary>
        /// Gets items by state
        /// </summary>
        /// <param name="state">State value</param>
        /// <param name="additionalFilters">Optional additional filters</param>
        /// <returns>Collection of items</returns>
        Task<IEnumerable<Item>> GetByStateAsync(AlState state, FilterParameters additionalFilters = null);

        /// <summary>
        /// Searches items by text
        /// </summary>
        /// <param name="searchText">Search text</param>
        /// <returns>Collection of matching items</returns>
        Task<IEnumerable<Item>> SearchByTextAsync(string searchText);

        /// <summary>
        /// Gets paginated items
        /// </summary>
        /// <param name="filter">Filter parameters</param>
        /// <returns>Paginated result</returns>
        Task<PaginatedResult<Item>> GetPaginatedAsync(FilterParameters filter);

        /// <summary>
        /// Gets paginated items by criteria
        /// </summary>
        /// <param name="criteria">Filter criteria</param>
        /// <returns>Paginated result</returns>
        Task<PaginatedResult<Item>> GetPaginatedByCriteriaAsync(FilterCriteria criteria);

        /// <summary>
        /// Increments the view count for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> IncrementViewCountAsync(string itemId);

        /// <summary>
        /// Increments the inquiry count for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> IncrementInquiryCountAsync(string itemId);

        /// <summary>
        /// Gets statistics for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Item statistics or null if not found</returns>
        Task<ItemStatistics> GetStatisticsAsync(string itemId);

        /// <summary>
        /// Updates the status of an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <param name="status">New status</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateStatusAsync(string itemId, ItemStatus status);

        /// <summary>
        /// Checks if an item is available
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if available</returns>
        Task<bool> IsAvailableAsync(string itemId);
    }
}