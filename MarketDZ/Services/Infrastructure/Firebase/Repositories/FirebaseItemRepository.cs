using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using LiteDB;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Services.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase-specific implementation of the Item repository
    /// </summary>
    public class FirebaseItemRepository : BaseRepository<Item, FirebaseItem>, IItemRepository
    {
        private readonly IFirebaseTransactionHelper _transactionHelper;
        private readonly IFirebaseIndexManager _indexManager;
        private readonly IFirebaseQueryOptimizer _queryOptimizer;

        public FirebaseItemRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<Item, FirebaseItem> entityMapper,
            ILogger<FirebaseItemRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper,
            IFirebaseIndexManager indexManager,
            IFirebaseQueryOptimizer queryOptimizer)
            : base(dataStore, entityMapper, logger, cacheService, "items")
        {
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
            _indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
            _queryOptimizer = queryOptimizer ?? throw new ArgumentNullException(nameof(queryOptimizer));
        }

        /// <summary>
        /// Creates a new item with proper indexing
        /// </summary>
        public override async Task<string> CreateAsync(Item item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            // Set default values if needed
            if (item.ListedDate == default)
            {
                item.ListedDate = DateTime.UtcNow;
            }

            if (item.Status == default)
            {
                item.Status = ItemStatus.Active;
            }

            try
            {
                // Generate ID if needed
                if (string.IsNullOrEmpty(item.Id))
                {
                    item.Id = Guid.NewGuid().ToString();
                }

                // Convert to Firebase entity
                var firebaseItem = EntityMapper.ToEntity(item);

                // Create or update in a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Save main entity
                await transaction.SetEntityAsync($"{CollectionPath}/{item.Id}", firebaseItem);

                // Create indexes
                var indexUpdates = ((FirebaseItem)firebaseItem).CreateIndexEntries();
                foreach (var update in indexUpdates)
                {
                    await transaction.SetEntityAsync(update.Key, update.Value);
                }

                await transaction.CommitAsync();

                // Update cache
                string cacheKey = $"{CollectionPath}_{item.Id}";
                CacheService?.AddToCache(cacheKey, item, DefaultCacheDuration);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return item.Id;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error creating item: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing item and maintains indexes
        /// </summary>
        public override async Task<bool> UpdateAsync(Item item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            if (string.IsNullOrEmpty(item.Id))
            {
                Logger.LogWarning("Cannot update item with null or empty ID");
                return false;
            }

            try
            {
                // Get existing item to compare for index changes
                var existingEntity = await DataStore.GetEntityAsync<FirebaseItem>($"{CollectionPath}/{item.Id}");
                if (existingEntity == null)
                {
                    Logger.LogWarning($"Item with ID {item.Id} not found for update");
                    return false;
                }

                // Convert domain to entity
                var updatedEntity = EntityMapper.ToEntity(item);

                // Handle versioning
                if (updatedEntity is IVersionedEntity versionedEntity)
                {
                    versionedEntity.Version = existingEntity is IVersionedEntity existingVersioned
                        ? existingVersioned.Version + 1
                        : 1;
                    versionedEntity.LastModified = DateTime.UtcNow;
                }

                // Use a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Remove old indexes
                var oldIndexes = existingEntity.CreateIndexEntries();
                foreach (var index in oldIndexes)
                {
                    await transaction.DeleteEntityAsync(index.Key);
                }

                // Save updated entity
                await transaction.SetEntityAsync($"{CollectionPath}/{item.Id}", updatedEntity);

                // Create new indexes
                var newIndexes = ((FirebaseItem)updatedEntity).CreateIndexEntries();
                foreach (var index in newIndexes)
                {
                    await transaction.SetEntityAsync(index.Key, index.Value);
                }

                await transaction.CommitAsync();

                // Update cache
                string cacheKey = $"{CollectionPath}_{item.Id}";
                CacheService?.InvalidateCache(cacheKey);
                CacheService?.AddToCache(cacheKey, item, DefaultCacheDuration);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating item with ID {item.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes an item and its indexes
        /// </summary>
        public override async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Logger.LogWarning("Cannot delete item with null or empty ID");
                return false;
            }

            try
            {
                // Get existing item to remove indexes
                var existingEntity = await DataStore.GetEntityAsync<FirebaseItem>($"{CollectionPath}/{id}");
                if (existingEntity == null)
                {
                    Logger.LogWarning($"Item with ID {id} not found for deletion");
                    return false;
                }

                // Use a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Remove indexes
                var indexes = existingEntity.CreateIndexEntries();
                foreach (var index in indexes)
                {
                    await transaction.DeleteEntityAsync(index.Key);
                }

                // Delete main entity
                await transaction.DeleteEntityAsync($"{CollectionPath}/{id}");

                await transaction.CommitAsync();

                // Update cache
                string cacheKey = $"{CollectionPath}_{id}";
                CacheService?.InvalidateCache(cacheKey);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error deleting item with ID {id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets items by user ID
        /// </summary>
        public async Task<IEnumerable<Item>> GetByUserIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Logger.LogWarning("Cannot get items for null or empty user ID");
                return Enumerable.Empty<Item>();
            }

            string cacheKey = $"user_{userId}_items";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Use the index for user items
                var userItemsPath = $"user_items/{userId}";
                var itemReferences = await DataStore.GetCollectionAsync<Dictionary<string, object>>(userItemsPath);

                if (itemReferences == null || !itemReferences.Any())
                {
                    return Enumerable.Empty<Item>();
                }

                // Extract item IDs from the references
                var itemIds = itemReferences.Select(r => r.Keys.FirstOrDefault()).Where(id => !string.IsNullOrEmpty(id)).ToList();

                // Get items in parallel
                var tasks = itemIds.Select(GetByIdAsync);
                var items = await Task.WhenAll(tasks);

                // Filter out nulls
                var result = items.Where(i => i != null).ToList();

                // Cache the result
                CacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting items for user ID {userId}: {ex.Message}");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Gets filtered items using an optimized query approach
        /// </summary>
        public async Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            string cacheKey = $"items_filtered_{filter.GetCacheKey()}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<Item>>(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            try
            {
                // Use the query optimizer to efficiently fetch filtered items
                var result = await _queryOptimizer.OptimizeQueryAsync(filter);

                // Cache the result
                CacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting filtered items: {ex.Message}");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Gets filtered items using filter criteria
        /// </summary>
        public async Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria)
        {
            if (criteria == null)
            {
                throw new ArgumentNullException(nameof(criteria));
            }

            // Convert criteria to parameters
            var parameters = criteria.ToFilterParameters();
            return await GetFilteredAsync(parameters);
        }

        /// <summary>
        /// Gets items by category
        /// </summary>
        public async Task<IEnumerable<Item>> GetByCategoryAsync(string category, FilterParameters additionalFilters = null)
        {
            if (string.IsNullOrEmpty(category))
            {
                Logger.LogWarning("Cannot get items for null or empty category");
                return Enumerable.Empty<Item>();
            }

            string cacheKey = $"items_category_{category}";
            if (additionalFilters != null)
            {
                cacheKey += $"_{additionalFilters.GetCacheKey()}";
            }

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Create filter parameters
                var filter = additionalFilters ?? new FilterParameters();
                filter.Category = category;

                // Use query optimizer to fetch items
                var result = await _queryOptimizer.OptimizeQueryAsync(filter);

                // Cache the result
                CacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting items for category {category}: {ex.Message}");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Gets items by state
        /// </summary>
        public async Task<IEnumerable<Item>> GetByStateAsync(AlState state, FilterParameters additionalFilters = null)
        {
            string stateValue = state.ToString();
            string cacheKey = $"items_state_{stateValue}";
            if (additionalFilters != null)
            {
                cacheKey += $"_{additionalFilters.GetCacheKey()}";
            }

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Create filter parameters
                var filter = additionalFilters ?? new FilterParameters();
                filter.State = state;

                // Use query optimizer to fetch items
                var result = await _queryOptimizer.OptimizeQueryAsync(filter);

                // Cache the result
                CacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting items for state {state}: {ex.Message}");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Searches items by text
        /// </summary>
        public async Task<IEnumerable<Item>> SearchByTextAsync(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                return await GetAllAsync(0, 50);
            }

            string cacheKey = $"items_search_{searchText.ToLowerInvariant()}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Create filter parameters
                var filter = new FilterParameters
                {
                    SearchText = searchText,
                    Status = ItemStatus.Active,
                    SortBy = SortOption.Relevance
                };

                // Use query optimizer to fetch items
                var result = await _queryOptimizer.OptimizeQueryAsync(filter);

                // For text search, we need to perform client-side relevance scoring
                var scoredItems = result
                    .Select(item => new
                    {
                        Item = item,
                        Score = CalculateSearchRelevance(item, searchText)
                    })
                    .OrderByDescending(x => x.Score)
                    .Select(x => x.Item)
                    .ToList();

                // Cache the result
                CacheService?.AddToCache(cacheKey, scoredItems, TimeSpan.FromMinutes(5));

                return scoredItems;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error searching items with text '{searchText}': {ex.Message}");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Gets paginated items using filter parameters
        /// </summary>
        public async Task<PaginatedResult<Item>> GetPaginatedAsync(FilterParameters filter)
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            string cacheKey = $"items_paginated_{filter.GetCacheKey()}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<PaginatedResult<Item>>(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }

            try
            {
                // Use query optimizer to fetch paginated items
                var result = await _queryOptimizer.OptimizePaginatedQueryAsync(filter);

                // Cache the result
                CacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(2));

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting paginated items: {ex.Message}");
                return new PaginatedResult<Item>(new List<Item>(), 0, filter.Page, filter.Take ?? 20);
            }
        }

        /// <summary>
        /// Gets paginated items using filter criteria
        /// </summary>
        public async Task<PaginatedResult<Item>> GetPaginatedByCriteriaAsync(FilterCriteria criteria)
        {
            if (criteria == null)
            {
                throw new ArgumentNullException(nameof(criteria));
            }

            // Convert criteria to parameters
            var parameters = criteria.ToFilterParameters();
            return await GetPaginatedAsync(parameters);
        }

        /// <summary>
        /// Increments the view count for an item atomically
        /// </summary>
        public async Task<bool> IncrementViewCountAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            try
            {
                // Use atomic increment helper
                var result = await _transactionHelper.AtomicIncrementAsync(
                    $"{CollectionPath}/{itemId}",
                    "viewCount",
                    1,
                    3);

                if (result)
                {
                    // Invalidate item cache
                    string cacheKey = $"{CollectionPath}_{itemId}";
                    CacheService?.InvalidateCache(cacheKey);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error incrementing view count for item {itemId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Increments the inquiry count for an item atomically
        /// </summary>
        public async Task<bool> IncrementInquiryCountAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            try
            {
                // Use atomic increment helper
                var result = await _transactionHelper.AtomicIncrementAsync(
                    $"{CollectionPath}/{itemId}",
                    "inquiryCount",
                    1,
                    3);

                if (result)
                {
                    // Invalidate item cache
                    string cacheKey = $"{CollectionPath}_{itemId}";
                    CacheService?.InvalidateCache(cacheKey);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error incrementing inquiry count for item {itemId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets statistics for an item
        /// </summary>
        public async Task<ItemStatistics> GetStatisticsAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            string cacheKey = $"item_stats_{itemId}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<ItemStatistics>(cacheKey, out var cachedStats))
            {
                return cachedStats;
            }

            try
            {
                // Get the item
                var item = await GetByIdAsync(itemId);
                if (item == null)
                {
                    return null;
                }

                // Get ratings for the item
                var ratingsPath = $"item_ratings/{itemId}";
                var ratings = await DataStore.GetCollectionAsync<Dictionary<string, object>>(ratingsPath);

                // Calculate statistics
                var stats = new ItemStatistics
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = itemId,
                    Item = item,
                    ViewCount = item.ViewCount,
                    InquiryCount = item.InquiryCount,
                    FavoriteCount = item.FavoriteCount,
                    FirstViewedAt = item.ListedDate, // Assuming first view is listing date
                    LastViewedAt = DateTime.UtcNow,
                    TotalTimeOnMarket = DateTime.UtcNow - item.ListedDate,
                    DaysListed = (int)(DateTime.UtcNow - item.ListedDate).TotalDays
                };

                // Process ratings if available
                if (ratings != null && ratings.Any())
                {
                    var ratingScores = ratings
                        .Where(r => r.Value is Dictionary<string, object> data && data.ContainsKey("score"))
                        .Select(r => {
                            var data = (Dictionary<string, object>)r.Value;
                            return Convert.ToDouble(data["score"]);
                        })
                        .ToList();

                    if (ratingScores.Any())
                    {
                        stats.AverageRating = ratingScores.Average();
                        stats.RatingCount = ratingScores.Count;
                    }
                }

                // Cache the result
                CacheService?.AddToCache(cacheKey, stats, TimeSpan.FromMinutes(10));

                return stats;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting statistics for item {itemId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates the status of an item
        /// </summary>
        public async Task<bool> UpdateStatusAsync(string itemId, ItemStatus status)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            try
            {
                // Use atomic update
                return await AtomicUpdateAsync(itemId, item => {
                    item.Status = status;
                    return item;
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating status for item {itemId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if an item is available
        /// </summary>
        public async Task<bool> IsAvailableAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            try
            {
                // Get the item
                var item = await GetByIdAsync(itemId);

                // Item is available if it exists and has Active status
                return item != null && item.Status == ItemStatus.Active;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error checking availability for item {itemId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Calculates search relevance score for item-text pair
        /// </summary>
        private double CalculateSearchRelevance(Item item, string searchText)
        {
            if (item == null || string.IsNullOrEmpty(searchText))
            {
                return 0;
            }

            // Normalize search terms
            var terms = searchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!terms.Any())
            {
                return 0;
            }

            double score = 0;

            // Check title (highest weight)
            var titleLower = item.Title.ToLowerInvariant();
            foreach (var term in terms)
            {
                if (titleLower.Contains(term))
                {
                    score += 10;
                    // Exact match gets higher score
                    if (titleLower.Equals(term))
                    {
                        score += 15;
                    }
                    // Word boundary match gets higher score
                    else if (titleLower.StartsWith(term + " ") ||
                             titleLower.EndsWith(" " + term) ||
                             titleLower.Contains(" " + term + " "))
                    {
                        score += 5;
                    }
                }
            }

            // Check description (lower weight)
            var descLower = item.Description.ToLowerInvariant();
            foreach (var term in terms)
            {
                if (descLower.Contains(term))
                {
                    score += 3;
                    // Word boundary match gets higher score
                    if (descLower.StartsWith(term + " ") ||
                        descLower.EndsWith(" " + term) ||
                        descLower.Contains(" " + term + " "))
                    {
                        score += 1;
                    }
                }
            }

            // Recently listed items get a boost
            var daysSinceListing = (DateTime.UtcNow - item.ListedDate).TotalDays;
            if (daysSinceListing < 7)
            {
                score += (7 - daysSinceListing) / 7 * 5; // Max +5 for very recent items
            }

            return score;
        }
    }
}