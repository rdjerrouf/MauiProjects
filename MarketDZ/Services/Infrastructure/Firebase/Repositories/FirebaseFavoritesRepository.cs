using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase-specific implementation of the Favorites repository
    /// </summary>
    public class FirebaseFavoritesRepository : IFavoritesRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseFavoritesRepository> _logger;
        private readonly ICacheService _cacheService;
        private readonly IFirebaseTransactionHelper _transactionHelper;
        private readonly IItemRepository _itemRepository;

        public FirebaseFavoritesRepository(
            IAppCoreDataStore dataStore,
            ILogger<FirebaseFavoritesRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper,
            IItemRepository itemRepository)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cacheService = cacheService;
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
        }

        /// <summary>
        /// Adds an item to a user's favorites
        /// </summary>
        public async Task<bool> AddAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId))
            {
                _logger.LogWarning($"Cannot add to favorites with null or empty userId: {userId} or itemId: {itemId}");
                return false;
            }

            try
            {
                // Check if already favorited
                if (await IsItemFavoritedAsync(userId, itemId))
                {
                    return true; // Already favorited, consider it a success
                }

                // Create favorite object
                var favorite = new UserFavorite
                {
                    UserId = userId,
                    ItemId = itemId,
                    DateAdded = DateTime.UtcNow
                };

                // Save to favorites
                return await CreateAsync(favorite);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding item {itemId} to favorites for user {userId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes an item from a user's favorites
        /// </summary>
        public async Task<bool> RemoveAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId))
            {
                _logger.LogWarning($"Cannot remove from favorites with null or empty userId: {userId} or itemId: {itemId}");
                return false;
            }

            try
            {
                // Create a unique ID for the favorite record (composite key)
                var favoriteId = $"{userId}_{itemId}";

                // Use a transaction for consistency
                using var transaction = await _dataStore.BeginTransactionAsync();

                // Get the favorite entity to ensure it exists
                var favoriteEntity = await transaction.GetEntityAsync<FirebaseFavorite>($"favorites/{favoriteId}");
                if (favoriteEntity == null)
                {
                    // Not found, no need to do anything
                    return true;
                }

                // Create removal entries
                var removals = favoriteEntity.CreateRemovalEntries();

                // Apply all removals
                foreach (var removal in removals)
                {
                    await transaction.DeleteEntityAsync(removal.Key);
                }

                // Decrement the item's favorite count
                var item = await transaction.GetEntityAsync<Dictionary<string, object>>($"items/{itemId}");
                if (item != null)
                {
                    int favoriteCount = 0;
                    if (item.ContainsKey("favoriteCount") && item["favoriteCount"] != null)
                    {
                        int.TryParse(item["favoriteCount"].ToString(), out favoriteCount);
                    }

                    item["favoriteCount"] = Math.Max(0, favoriteCount - 1); // Ensure not negative
                    await transaction.SetEntityAsync($"items/{itemId}", item);
                }

                await transaction.CommitAsync();

                // Invalidate caches
                InvalidateFavoriteCache(userId, itemId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing item {itemId} from favorites for user {userId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a user's favorite item IDs
        /// </summary>
        public async Task<List<string>> GetByUserIdAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot get favorites for null or empty user ID");
                return new List<string>();
            }

            string cacheKey = $"user_{userId}_favorites";

            // Try to get from cache
            if (_cacheService != null && _cacheService.TryGetFromCache<List<string>>(cacheKey, out var cachedFavorites))
            {
                return cachedFavorites;
            }

            try
            {
                // Get favorite entries
                var favoritesPath = $"user_favorites/{userId}";
                var favorites = await _dataStore.GetCollectionAsync<object>(favoritesPath);

                if (favorites == null || !favorites.Any())
                {
                    return new List<string>();
                }

                // Extract item IDs from the keys
                var result = new List<string>();

                foreach (var favorite in favorites)
                {
                    // The key is the path segment after the last slash
                    string path = favorite.GetType().GetProperty("Key")?.GetValue(favorite)?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        string itemId = path.Split('/').LastOrDefault();
                        if (!string.IsNullOrEmpty(itemId))
                        {
                            result.Add(itemId);
                        }
                    }
                }

                // Cache the result
                _cacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(10));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting favorites for user {userId}: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Checks if an item is in a user's favorites
        /// </summary>
        public async Task<bool> IsItemFavoritedAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            string cacheKey = $"user_{userId}_favorite_{itemId}";

            // Try to get from cache
            if (_cacheService != null && _cacheService.TryGetFromCache<bool>(cacheKey, out var isFavorited))
            {
                return isFavorited;
            }

            try
            {
                // Check if favorite exists
                var favorite = await _dataStore.GetEntityAsync<object>($"user_favorites/{userId}/{itemId}");

                bool result = favorite != null;

                // Cache the result
                _cacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(10));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if item {itemId} is favorited by user {userId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets detailed items for a user's favorites
        /// </summary>
        public async Task<List<Item>> GetFavoriteItemsAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Cannot get favorite items for null or empty user ID");
                return new List<Item>();
            }

            string cacheKey = $"user_{userId}_favorite_items";

            // Try to get from cache
            if (_cacheService != null && _cacheService.TryGetFromCache<List<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Get favorite item IDs
                var itemIds = await GetByUserIdAsync(userId);

                if (itemIds == null || !itemIds.Any())
                {
                    return new List<Item>();
                }

                // Get items in parallel
                var tasks = itemIds.Select(_itemRepository.GetByIdAsync);
                var items = await Task.WhenAll(tasks);

                // Filter out nulls
                var result = items.Where(item => item != null).ToList();

                // Cache the result
                _cacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(5));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting favorite items for user {userId}: {ex.Message}");
                return new List<Item>();
            }
        }

        /// <summary>
        /// Gets the users who have favorited an item
        /// </summary>
        public async Task<List<string>> GetUsersByItemIdAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                _logger.LogWarning("Cannot get users for null or empty item ID");
                return new List<string>();
            }

            string cacheKey = $"item_{itemId}_favorited_by";

            // Try to get from cache
            if (_cacheService != null && _cacheService.TryGetFromCache<List<string>>(cacheKey, out var cachedUsers))
            {
                return cachedUsers;
            }

            try
            {
                // Get user entries
                var usersPath = $"item_favorites/{itemId}";
                var users = await _dataStore.GetCollectionAsync<object>(usersPath);

                if (users == null || !users.Any())
                {
                    return new List<string>();
                }

                // Extract user IDs from the keys
                var result = new List<string>();

                foreach (var user in users)
                {
                    // The key is the path segment after the last slash
                    string path = user.GetType().GetProperty("Key")?.GetValue(user)?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        string userId = path.Split('/').LastOrDefault();
                        if (!string.IsNullOrEmpty(userId))
                        {
                            result.Add(userId);
                        }
                    }
                }

                // Cache the result
                _cacheService?.AddToCache(cacheKey, result, TimeSpan.FromMinutes(10));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users who favorited item {itemId}: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Creates a user favorite entry
        /// </summary>
        public async Task<bool> CreateAsync(UserFavorite favorite)
        {
            if (favorite == null)
            {
                throw new ArgumentNullException(nameof(favorite));
            }

            if (string.IsNullOrEmpty(favorite.UserId) || string.IsNullOrEmpty(favorite.ItemId))
            {
                _logger.LogWarning("Cannot create favorite with null or empty User ID or Item ID");
                return false;
            }

            try
            {
                // Create a unique ID for the favorite record (composite key)
                var favoriteId = $"{favorite.UserId}_{favorite.ItemId}";
                favorite.Id = favoriteId;

                // Set created date if not set
                if (favorite.DateAdded == default)
                {
                    favorite.DateAdded = DateTime.UtcNow;
                }

                // Convert to Firebase entity
                var firebaseFavorite = FirebaseFavorite.FromUserFavorite(favorite, favoriteId);

                // Use a transaction to ensure consistency
                using var transaction = await _dataStore.BeginTransactionAsync();

                // Save main entity
                await transaction.SetEntityAsync($"favorites/{favoriteId}", firebaseFavorite);

                // Create indexes
                var indexUpdates = firebaseFavorite.CreateIndexEntries();
                foreach (var update in indexUpdates)
                {
                    await transaction.SetEntityAsync(update.Key, update.Value);
                }

                // Increment the item's favorite count
                var item = await transaction.GetEntityAsync<Dictionary<string, object>>($"items/{favorite.ItemId}");
                if (item != null)
                {
                    int favoriteCount = 0;
                    if (item.ContainsKey("favoriteCount") && item["favoriteCount"] != null)
                    {
                        int.TryParse(item["favoriteCount"].ToString(), out favoriteCount);
                    }

                    item["favoriteCount"] = favoriteCount + 1;
                    await transaction.SetEntityAsync($"items/{favorite.ItemId}", item);
                }

                await transaction.CommitAsync();

                // Invalidate caches
                InvalidateFavoriteCache(favorite.UserId, favorite.ItemId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating favorite for User {favorite.UserId} and Item {favorite.ItemId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Invalidates favorite-related caches
        /// </summary>
        private void InvalidateFavoriteCache(string userId, string itemId)
        {
            if (_cacheService == null)
            {
                return;
            }

            // Invalidate specific favorite cache
            string favoriteCacheKey = $"user_{userId}_favorite_{itemId}";
            _cacheService.InvalidateCache(favoriteCacheKey);

            // Invalidate user favorites list cache
            string userFavoritesCacheKey = $"user_{userId}_favorites";
            _cacheService.InvalidateCache(userFavoritesCacheKey);

            // Invalidate user favorite items cache
            string userFavoriteItemsCacheKey = $"user_{userId}_favorite_items";
            _cacheService.InvalidateCache(userFavoriteItemsCacheKey);

            // Invalidate item favorited by users cache
            string itemFavoritedByCacheKey = $"item_{itemId}_favorited_by";
            _cacheService.InvalidateCache(itemFavoritedByCacheKey);

            // Invalidate item cache
            string itemCacheKey = $"items_{itemId}";
            _cacheService.InvalidateCache(itemCacheKey);
        }
    }
}