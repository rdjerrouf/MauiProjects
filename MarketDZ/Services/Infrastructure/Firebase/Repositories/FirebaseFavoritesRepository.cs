using MarketDZ.Models;
using MarketDZ.Models.Firebase.Base.Adapters;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using MarketDZ.Services.DbServices;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using MarketDZ.Services.Repositories;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase implementation of IFavoritesRepository
    /// </summary>
    public class FirebaseFavoritesRepository : IFavoritesRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseFavoritesRepository> _logger;
        private readonly IItemRepository _itemRepository;
        private const string FavoritesPath = "favorites";

        /// <summary>
        /// Creates a new Firebase favorites repository
        /// </summary>
        public FirebaseFavoritesRepository(
            IAppCoreDataStore dataStore,
            IItemRepository itemRepository,
            ILogger<FirebaseFavoritesRepository> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Add an item to a user's favorites
        /// </summary>
        public async Task<bool> AddAsync(string userId, string itemId)
        {
            try
            {
                _logger.LogInformation($"Adding item {itemId} to favorites for user {userId}");

                // First verify that the item exists
                var itemExists = await _itemRepository.GetByIdAsync(itemId) != null;
                if (!itemExists)
                {
                    _logger.LogWarning($"Cannot add favorite: Item {itemId} not found");
                    return false;
                }

                // Create a domain model
                var favorite = new UserFavorite
                {
                    UserId = userId,
                    ItemId = itemId,
                    DateAdded = DateTime.UtcNow
                };

                // Convert to Firebase model
                var firebaseFavorite = FirebaseFavorite.FromUserFavorite(favorite);

                // Create multi-path update including indexes
                var updates = new Dictionary<string, object>
                {
                    [$"{FavoritesPath}/{firebaseFavorite.Id}"] = firebaseFavorite.ToFirebaseObject()
                };

                // Add all index entries
                foreach (var entry in firebaseFavorite.CreateIndexEntries())
                {
                    updates[entry.Key] = entry.Value;
                }

                // Also update favorite count on the item
                updates[$"items/{itemId}/favoriteCount"] = await GetFavoriteCountAsync(itemId) + 1;

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding item {itemId} to favorites for user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Remove an item from a user's favorites
        /// </summary>
        public async Task<bool> RemoveAsync(string userId, string itemId)
        {
            try
            {
                _logger.LogInformation($"Removing item {itemId} from favorites for user {userId}");

                // Create a temporary Firebase favorite just to generate the removal entries
                var firebaseFavorite = new FirebaseFavorite
                {
                    Id = $"{userId}_{itemId}",
                    UserId = userId,
                    ItemId = itemId
                };

                // Get all paths that need to be removed (including indexes)
                var updates = firebaseFavorite.CreateRemovalEntries();

                // Also update favorite count on the item
                int currentCount = await GetFavoriteCountAsync(itemId);
                updates[$"items/{itemId}/favoriteCount"] = Math.Max(0, currentCount - 1);

                // Execute as atomic update (null values will remove the nodes)
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing item {itemId} from favorites for user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Get a user's favorite item IDs
        /// </summary>
        public async Task<List<string>> GetByUserIdAsync(string userId)
        {
            try
            {
                // Use the denormalized user_favorites index
                var favorites = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"user_favorites/{userId}");

                if (favorites == null || !favorites.Any())
                    return new List<string>();

                // Extract item IDs from the keys - fixed to handle nullable properly
                return favorites
                    .Select(f => f.Keys.FirstOrDefault())
                    .Where(k => k != null)
                    .ToList(); // Keys are already strings
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting favorites for user {userId}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Check if an item is in a user's favorites
        /// </summary>
        public async Task<bool> IsItemFavoritedAsync(string userId, string itemId)
        {
            try
            {
                // Check directly in the user_favorites index
                var favorite = await _dataStore.GetEntityAsync<object>($"user_favorites/{userId}/{itemId}");
                return favorite != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if item {itemId} is favorited by user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Get detailed items for a user's favorites with batch loading
        /// </summary>
        public async Task<List<Item>> GetFavoriteItemsAsync(string userId)
        {
            try
            {
                // Get the IDs of the user's favorites
                var favoriteItemIds = await GetByUserIdAsync(userId);

                if (favoriteItemIds.Count == 0)
                {
                    _logger.LogInformation($"No favorite items found for user {userId}");
                    return new List<Item>();
                }

                // Use parallel batch loading for better performance
                const int batchSize = 20; // Configurable batch size for optimized loading
                var items = new List<Item>();

                for (int i = 0; i < favoriteItemIds.Count; i += batchSize)
                {
                    var batch = favoriteItemIds.Skip(i).Take(batchSize);
                    var tasks = batch.Select(itemId => _itemRepository.GetByIdAsync(itemId));
                    var batchResults = await Task.WhenAll(tasks);

                    // Collect non-null results
                    items.AddRange(batchResults.Where(item => item != null)!);
                }

                _logger.LogInformation($"Loaded {items.Count} favorite items for user {userId}");
                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting favorite items for user {userId}");
                return new List<Item>();
            }
        }

        /// <summary>
        /// Get the users who have favorited an item
        /// </summary>
        public async Task<List<string>> GetUsersByItemIdAsync(string itemId)
        {
            try
            {
                // Use the denormalized item_favorites index
                var favorites = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"item_favorites/{itemId}");

                if (favorites == null || !favorites.Any())
                    return new List<string>();

                // Extract user IDs from the keys
                return favorites
                    .Select(f => f.Keys.FirstOrDefault())
                    .Where(k => k != null)
                    .ToList(); // Keys are already strings
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting users who favorited item {itemId}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Create a user favorite entry
        /// </summary>
        public async Task<bool> CreateAsync(UserFavorite favorite)
        {
            try
            {
                if (favorite == null)
                    throw new ArgumentNullException(nameof(favorite));

                if (string.IsNullOrEmpty(favorite.UserId) || string.IsNullOrEmpty(favorite.ItemId))
                    throw new ArgumentException("UserId and ItemId must be valid");

                // Set the date if not already set
                if (favorite.DateAdded == default)
                {
                    favorite.DateAdded = DateTime.UtcNow;
                }

                // Convert to Firebase model
                var firebaseFavorite = FirebaseFavorite.FromUserFavorite(favorite);

                // Create multi-path update including indexes
                var updates = new Dictionary<string, object>
                {
                    [$"{FavoritesPath}/{firebaseFavorite.Id}"] = firebaseFavorite.ToFirebaseObject()
                };

                // Add all index entries
                foreach (var entry in firebaseFavorite.CreateIndexEntries())
                {
                    updates[entry.Key] = entry.Value;
                }

                // Also update favorite count on the item
                updates[$"items/{favorite.ItemId}/favoriteCount"] = await GetFavoriteCountAsync(favorite.ItemId) + 1;

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating favorite for user {favorite?.UserId} and item {favorite?.ItemId}");
                return false;
            }
        }

        /// <summary>
        /// Get the current favorite count for an item
        /// </summary>
        private async Task<int> GetFavoriteCountAsync(string itemId)
        {
            try
            {
                var users = await GetUsersByItemIdAsync(itemId);
                return users.Count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Add a favorite atomically with item counter increment
        /// </summary>
        public async Task<bool> AddAtomicAsync(string userId, string itemId)
        {
            try
            {
                using var transaction = await _dataStore.BeginTransactionAsync();

                // Check if favorite already exists
                var favoriteKey = $"{userId}_{itemId}";
                var existingFavorite = await transaction.GetEntityAsync<FirebaseFavorite>($"{FavoritesPath}/{favoriteKey}");

                if (existingFavorite != null)
                {
                    return true; // Already favorited
                }

                // Validate item exists
                var item = await transaction.GetEntityAsync<FirebaseItem>($"items/{itemId}");
                if (item == null)
                {
                    _logger.LogWarning($"Cannot add favorite: Item {itemId} not found");
                    return false;
                }

                // Create favorite entry
                var favorite = new UserFavorite
                {
                    UserId = userId,
                    ItemId = itemId,
                    DateAdded = DateTime.UtcNow
                };

                var firebaseFavorite = FirebaseFavorite.FromUserFavorite(favorite);

                // Set all related data as part of the transaction
                await transaction.SetEntityAsync($"{FavoritesPath}/{firebaseFavorite.Id}", firebaseFavorite);
                await transaction.SetEntityAsync($"user_favorites/{userId}/{itemId}", true);
                await transaction.SetEntityAsync($"item_favorites/{itemId}/{userId}", true);

                // Increment item's favorite count and handle version
                // Use reflection to avoid ambiguity between duplicated Version properties
                item.FavoriteCount += 1;
                item.LastModified = DateTime.UtcNow;

                // Access Version property using reflection to avoid ambiguity
                var versionProperty = typeof(FirebaseItem)
                    .GetProperties()
                    .FirstOrDefault(p => p.Name == "Version" && p.PropertyType == typeof(int));

                if (versionProperty != null)
                {
                    int currentVersion = (int)versionProperty.GetValue(item)!;
                    versionProperty.SetValue(item, currentVersion + 1);
                }

                await transaction.SetEntityAsync($"items/{itemId}", item);

                await transaction.CommitAsync();
                return true;
            }
            catch (FirebaseException ex) when (ex.Message.Contains("concurrent"))
            {
                // Handle concurrency conflict - possibly retry
                _logger.LogWarning($"Concurrency conflict while adding favorite for user {userId}, item {itemId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding favorite for user {userId}, item {itemId}");
                return false;
            }
        }
    }
}