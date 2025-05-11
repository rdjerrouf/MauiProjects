using System.Collections.Generic;
using System.Text;
using MarketDZ.Models;
using MarketDZ.Models.Filters;
using MarketDZ.Services.DbServices.Extensions;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    public class CacheAwareItemRepository : IItemRepository
    {
        private readonly IItemRepository _innerRepository;
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheAwareItemRepository> _logger;

        public CacheAwareItemRepository(
            IItemRepository innerRepository,
            ICacheService cacheService,
            ILogger<CacheAwareItemRepository> logger)
        {
            _innerRepository = innerRepository ?? throw new ArgumentNullException(nameof(innerRepository));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Updated methods to match interface return types
        // For handling ItemCategory to string conversion issues

        private string GetCategoryString(ItemCategory? category)
        {
            return category?.ToString() ?? string.Empty;
        }
        public async Task<IEnumerable<Item>> SearchByTextAsync(string searchText)
        {
            var cacheKey = $"search_text:{searchText}";

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for search text: {SearchText}", searchText);
                return cachedItems;
            }

            var items = await _innerRepository.SearchByTextAsync(searchText);

            _cacheService.AddToCache(cacheKey, items, CachePolicy.Volatile);

            return items;
        }

        public async Task<Item?> GetByIdAsync(string id)
        {
            var cacheKey = CacheExtensions.GetItemCacheKey(id);

            if (_cacheService.TryGetFromCache<Item>(cacheKey, out var cachedItem))
            {
                _logger.LogDebug("Cache hit for item {ItemId}", id);
                return cachedItem;
            }

            var item = await _innerRepository.GetByIdAsync(id);

            if (item != null)
            {
                _cacheService.AddToCache(cacheKey, item,
                    CacheExtensions.DetermineCachePolicy(cacheKey),
                    CacheExtensions.GetCacheDependencies(cacheKey));
            }

            return item;
        }

        public async Task<IEnumerable<Item>> GetAllAsync()
        {
            var cacheKey = "all_items";
            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for all items");
                return cachedItems;
            }

            var items = await _innerRepository.GetAllAsync();

            // Convert CachePolicy to TimeSpan
            _cacheService.AddToCache(cacheKey, items, ConvertCachePolicyToTimeSpan(CachePolicy.Moderate));

            return items;
        }
        public async Task<string> CreateAsync(Item item)
        {
            var id = await _innerRepository.CreateAsync(item);

            // Fix: Convert the integer ID to a string before checking for null or empty
            if (!string.IsNullOrEmpty(id.ToString()))
            {
                // Invalidate related caches
                _cacheService.InvalidateCache(CacheExtensions.GetUserItemsCacheKey(item.PostedByUserId));

                if (item.Category != null)
                {
                    _cacheService.InvalidateCache(CacheExtensions.GetCategoryItemsCacheKey(GetCategoryString(item.Category)));
                }

                _cacheService.InvalidateCache("all_items");
                _cacheService.InvalidateCachePattern("search:");
            }

            return id.ToString(); // Ensure the return type matches the method signature
        }

        public async Task<bool> UpdateAsync(Item item)
        {
            var result = await _innerRepository.UpdateAsync(item);

            if (result)
            {
                // Invalidate specific item cache
                _cacheService.InvalidateCache(CacheExtensions.GetItemCacheKey(item.Id));

                // Invalidate user's items cache
                _cacheService.InvalidateCache(CacheExtensions.GetUserItemsCacheKey(item.PostedByUserId));

                // Invalidate category cache - fixed conversion
                if (item.Category != null)
                {
                    _cacheService.InvalidateCache(CacheExtensions.GetCategoryItemsCacheKey(GetCategoryString(item.Category)));
                }

                // Invalidate all items cache and search results
                _cacheService.InvalidateCache("all_items");
                _cacheService.InvalidateCachePattern("search:");
            }

            return result;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            // Get item before deletion to invalidate related caches
            var item = await GetByIdAsync(id);

            var result = await _innerRepository.DeleteAsync(id);

            if (result && item != null)
            {
                // Invalidate all related caches
                _cacheService.InvalidateCache(CacheExtensions.GetItemCacheKey(id));
                _cacheService.InvalidateCache(CacheExtensions.GetUserItemsCacheKey(item.PostedByUserId));

                if (item.Category != null)
                {
                    _cacheService.InvalidateCache(CacheExtensions.GetCategoryItemsCacheKey(GetCategoryString(item.Category)));
                }

                _cacheService.InvalidateCache("all_items");
                _cacheService.InvalidateCachePattern("search:");
            }

            return result;
        }
        public async Task<IEnumerable<Item>> GetByUserIdAsync(string userId)
        {
            var cacheKey = CacheExtensions.GetUserItemsCacheKey(userId);

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for user {UserId} items", userId);
                return cachedItems;
            }

            var items = await _innerRepository.GetByUserIdAsync(userId);

            _cacheService.AddToCache(cacheKey, items,
                CacheExtensions.DetermineCachePolicy(cacheKey),
                CacheExtensions.GetCacheDependencies(cacheKey));

            return items;
        }

        public async Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters filter)
        {
            var cacheKey = CacheExtensions.GetSearchCacheKey(filter);

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for filtered items with parameters");
                return cachedItems;
            }

            var items = await _innerRepository.GetFilteredAsync(filter);

            _cacheService.AddToCache(cacheKey, items, CachePolicy.Volatile);

            return items;
        }

        public async Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria)
        {
            var cacheKey = $"filter_criteria:{GenerateCacheKey(criteria)}";

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for filtered items with criteria");
                return cachedItems;
            }

            var items = await _innerRepository.GetFilteredByCriteriaAsync(criteria);

            _cacheService.AddToCache(cacheKey, items, CachePolicy.Volatile);

            return items;
        }

        public async Task<IEnumerable<Item>> GetByCategoryAsync(ItemCategory category, FilterParameters? additionalFilters = null)
        {
            // Convert ItemCategory to string
            string categoryString = GetCategoryString(category);
            var cacheKey = CacheExtensions.GetCategoryItemsCacheKey(categoryString);

            if (additionalFilters != null)
            {
                cacheKey += $":{additionalFilters.GetCacheKey()}";
            }

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for category {Category} items", categoryString);
                return cachedItems;
            }

            // Call the inner repository with the converted category
            var items = await _innerRepository.GetByCategoryAsync(categoryString, additionalFilters);

            // Convert CachePolicy to TimeSpan
            TimeSpan cacheDuration = ConvertCachePolicyToTimeSpan(CachePolicy.Stable);
            _cacheService.AddToCache(cacheKey, items, cacheDuration);

            return items;
        }
        public async Task<IEnumerable<Item>> GetByStateAsync(AlState state, FilterParameters? additionalFilters = null)
        {
            var cacheKey = $"state_items:{state}";

            if (additionalFilters != null)
            {
                cacheKey += $":{additionalFilters.GetCacheKey()}";
            }

            if (_cacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                _logger.LogDebug("Cache hit for state {State} items", state);
                return cachedItems;
            }

            var items = await _innerRepository.GetByStateAsync(state, additionalFilters);

            _cacheService.AddToCache(cacheKey, items, CachePolicy.Stable);

            return items;
        }

        public async Task<PaginatedResult<Item>> GetPaginatedAsync(FilterParameters filter)
        {
            var cacheKey = $"paginated:{filter.GetCacheKey()}";

            if (_cacheService.TryGetFromCache<PaginatedResult<Item>>(cacheKey, out var cachedResult))
            {
                _logger.LogDebug("Cache hit for paginated items");
                return cachedResult;
            }

            var result = await _innerRepository.GetPaginatedAsync(filter);

            _cacheService.AddToCache(cacheKey, result, ConvertCachePolicyToTimeSpan(CachePolicy.Volatile));

            return result;
        }

        public async Task<PaginatedResult<Item>> GetPaginatedByCriteriaAsync(FilterCriteria criteria)
        {
            // Generate a cache key for the criteria
            var cacheKey = $"paginated_criteria:{GenerateCacheKey(criteria)}";

            if (_cacheService.TryGetFromCache<PaginatedResult<Item>>(cacheKey, out var cachedResult))
            {
                _logger.LogDebug("Cache hit for paginated items by criteria");
                return cachedResult;
            }

            var result = await _innerRepository.GetPaginatedByCriteriaAsync(criteria);

            _cacheService.AddToCache(cacheKey, result, ConvertCachePolicyToTimeSpan(CachePolicy.Volatile));

            return result;
        }

        public async Task<bool> IncrementViewCountAsync(string itemId)
        {
            var result = await _innerRepository.IncrementViewCountAsync(itemId);

            if (result)
            {
                // Invalidate item cache after view increment
                _cacheService.InvalidateCache(CacheExtensions.GetItemCacheKey(itemId));
            }

            return result;
        }

        public async Task<bool> IncrementInquiryCountAsync(string itemId)
        {
            var result = await _innerRepository.IncrementInquiryCountAsync(itemId);

            if (result)
            {
                // Invalidate item cache after inquiry increment
                _cacheService.InvalidateCache(CacheExtensions.GetItemCacheKey(itemId));
            }

            return result;
        }

        public async Task<ItemStatistics?> GetStatisticsAsync(string itemId)
        {
            var cacheKey = $"item_statistics:{itemId}";

            if (_cacheService.TryGetFromCache<ItemStatistics>(cacheKey, out var cachedStats))
            {
                _logger.LogDebug("Cache hit for item {ItemId} statistics", itemId);
                return cachedStats;
            }

            var stats = await _innerRepository.GetStatisticsAsync(itemId);

            if (stats != null)
            {
                _cacheService.AddToCache(cacheKey, stats, ConvertCachePolicyToTimeSpan(CachePolicy.Volatile));
            }

            return stats;
        }

        public async Task<bool> UpdateStatusAsync(string itemId, ItemStatus status)
        {
            var result = await _innerRepository.UpdateStatusAsync(itemId, status);

            if (result)
            {
                // Invalidate item cache and related caches
                _cacheService.InvalidateCache(CacheExtensions.GetItemCacheKey(itemId));

                // Get item to invalidate related caches
                var item = await _innerRepository.GetByIdAsync(itemId);
                if (item != null)
                {
                    _cacheService.InvalidateCache(CacheExtensions.GetUserItemsCacheKey(item.PostedByUserId));

                    if (item.Category != null)
                    {
                        _cacheService.InvalidateCache(CacheExtensions.GetCategoryItemsCacheKey(GetCategoryString(item.Category)));
                    }
                }

                _cacheService.InvalidateCachePattern("search:");
            }

            return result;
        }

        public async Task<bool> IsAvailableAsync(string itemId)
        {
            var cacheKey = $"item_available:{itemId}";

            if (_cacheService.TryGetFromCache<bool>(cacheKey, out var isAvailable))
            {
                _logger.LogDebug("Cache hit for item {ItemId} availability", itemId);
                return isAvailable;
            }

            var available = await _innerRepository.IsAvailableAsync(itemId);

            _cacheService.AddToCache(cacheKey, available, ConvertCachePolicyToTimeSpan(CachePolicy.Volatile));

            return available;
        }

        private string GenerateCacheKey(FilterCriteria criteria)
        {
            var keyBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(criteria.SearchText))
                keyBuilder.Append($"search:{criteria.SearchText};");

            if (criteria.Categories != null && criteria.Categories.Any())
                keyBuilder.Append($"categories:{string.Join(",", criteria.Categories)};");

            if (criteria.MinPrice.HasValue)
                keyBuilder.Append($"minPrice:{criteria.MinPrice.Value};");

            if (criteria.MaxPrice.HasValue)
                keyBuilder.Append($"maxPrice:{criteria.MaxPrice.Value};");

            if (criteria.State.HasValue)
                keyBuilder.Append($"state:{criteria.State.Value};");

            if (criteria.Status.HasValue)
                keyBuilder.Append($"status:{criteria.Status.Value};");

            if (criteria.Latitude.HasValue && criteria.Longitude.HasValue)
                keyBuilder.Append($"location:{criteria.Latitude.Value},{criteria.Longitude.Value};");

            if (criteria.RadiusKm.HasValue)
                keyBuilder.Append($"radius:{criteria.RadiusKm.Value};");

            if (criteria.SortByDistance)
                keyBuilder.Append("sortByDistance:true;");

            if (criteria.DateFrom.HasValue)
                keyBuilder.Append($"dateFrom:{criteria.DateFrom.Value:O};");

            if (criteria.DateTo.HasValue)
                keyBuilder.Append($"dateTo:{criteria.DateTo.Value:O};");

            if (criteria.SortBy.HasValue)
                keyBuilder.Append($"sortBy:{criteria.SortBy.Value};");

            if (criteria.Page > 0)
                keyBuilder.Append($"page:{criteria.Page};");

            if (criteria.PageSize > 0)
                keyBuilder.Append($"pageSize:{criteria.PageSize};");

            if (criteria.Tags != null && criteria.Tags.Any())
                keyBuilder.Append($"tags:{string.Join(",", criteria.Tags)};");

            if (criteria.ForSaleCategory.HasValue)
                keyBuilder.Append($"forSaleCategory:{criteria.ForSaleCategory.Value};");

            if (criteria.ForRentCategory.HasValue)
                keyBuilder.Append($"forRentCategory:{criteria.ForRentCategory.Value};");

            if (criteria.JobCategory.HasValue)
                keyBuilder.Append($"jobCategory:{criteria.JobCategory.Value};");

            if (criteria.ServiceCategory.HasValue)
                keyBuilder.Append($"serviceCategory:{criteria.ServiceCategory.Value};");

            return keyBuilder.ToString();
        }
        public Task<IEnumerable<Item>> GetByCategoryAsync(string category, FilterParameters? additionalFilters = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Helper method to convert CachePolicy to TimeSpan
        /// <summary/>
        private TimeSpan ConvertCachePolicyToTimeSpan(CachePolicy policy)
        {
            return policy switch
            {
                CachePolicy.Volatile => TimeSpan.FromMinutes(5),
                CachePolicy.Moderate => TimeSpan.FromHours(1),
                CachePolicy.Stable => TimeSpan.FromDays(1),
                _ => TimeSpan.FromMinutes(5) // Default to volatile if unknown
            };




        }

       
    }
}