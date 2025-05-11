using MarketDZ.Services.DbServices;
using MarketDZ.Models;
using MarketDZ.Models.Firebase.Base.Adapters;
using Microsoft.Extensions.Logging;
using static MarketDZ.Services.DbServices.Firebase.FirebaseTransaction;
using Google.Apis.Util;



namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase implementation of IRatingsRepository
    /// </summary>
    public class FirebaseRatingsRepository : IRatingsRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<FirebaseRatingsRepository> _logger;
        private const string RatingsPath = "ratings";
        private const string ItemsPath = "items";

        public FirebaseRatingsRepository(
            IAppCoreDataStore dataStore,
            IItemRepository itemRepository,
            ILogger<FirebaseRatingsRepository> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get a rating by its ID
        /// </summary>
        public async Task<Rating> GetByIdAsync(string ratingId)
        {
            try
            {
                var firebaseRating = await _dataStore.GetEntityAsync<FirebaseRating>($"{RatingsPath}/{ratingId}");
                return firebaseRating?.ToRating();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting rating with ID {ratingId}");
                return null;
            }
        }

        /// <summary>
        /// Get a rating by user and item IDs
        /// </summary>
        public async Task<Rating> GetByUserAndItemAsync(int userId, string itemId)
        {
            try
            {
                // Create the rating ID from the user and item IDs
                string ratingId = $"{itemId}_{userId}";
                return await GetByIdAsync(ratingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting rating for user {userId} and item {itemId}");
                return null;
            }
        }

        /// <summary>
        /// Get all ratings by a user
        /// </summary>
        public async Task<List<Rating>> GetByUserIdAsync(int userId)
        {
            try
            {
                // Use the denormalized user_ratings index
                var ratingEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"user_ratings/{userId}");

                if (ratingEntries == null || !ratingEntries.Any())
                    return new List<Rating>();

                // Get the full ratings in parallel
                var ratingTasks = ratingEntries.Select(async entry => {
                    // Each entry is a dictionary, and we need the key of this dictionary
                    // which is the itemId in this case
                    var itemId = entry.Keys.FirstOrDefault();
                    if (itemId == null) return null;
                    
                    string ratingId = $"{itemId}_{userId}";
                    return await GetByIdAsync(ratingId);
                });

                var ratings = await Task.WhenAll(ratingTasks);

                // Filter out nulls and sort by date
                return ratings
                    .Where(r => r != null)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings for user {userId}");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Get all ratings for an item
        /// </summary>
        public async Task<List<Rating>> GetByItemIdAsync(string itemId)
        {
            try
            {
                // Use the denormalized item_ratings index
                var ratingEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"item_ratings/{itemId}");

                if (ratingEntries == null || !ratingEntries.Any())
                    return new List<Rating>();

                // Get the full ratings in parallel
                // Fixed code to properly access dictionary key
                var ratingTasks = ratingEntries.Select(async entry => {
                    // Each entry is a dictionary, and we need the key of this dictionary
                    // which is the userId in this case
                    var userId = entry.Keys.FirstOrDefault();
                    if (userId == null) return null;
                    
                    string ratingId = $"{itemId}_{userId}";
                    return await GetByIdAsync(ratingId);
                });

                var ratings = await Task.WhenAll(ratingTasks);

                // Filter out nulls and sort by date
                return ratings
                    .Where(r => r != null)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings for item {itemId}");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Get all ratings for items owned by a user
        /// </summary>
        public async Task<List<Rating>> GetForUserItemsAsync(int userId)
        {
            try
            {
                // Fix for CS1503: Convert 'userId' from 'int' to 'string' when calling _itemRepository.GetByUserIdAsync
                var userItems = await _itemRepository.GetByUserIdAsync(userId.ToString());
                if (!userItems.Any())
                {
                    return new List<Rating>();
                }

                // Fix for CS8604: Ensure that `item.Id` is not null before calling `GetByItemIdAsync`
                var allRatingsTasks = userItems
                    .Where(item => item.Id != null) // Filter out items with null Id
                    .Select(item => GetByItemIdAsync(item.Id!)); // Use null-forgiving operator since nulls are filtered out
                var allRatingsByItem = await Task.WhenAll(allRatingsTasks);

                // Flatten and sort
                return allRatingsByItem
                    .SelectMany(r => r)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings for items owned by user {userId}");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Add a new rating
        /// </summary>
        public async Task<bool> AddAsync(Rating rating)
        {
            try
            {
                // Validate rating
                if (rating == null)
                {
                    throw new ArgumentNullException(nameof(rating));
                }

                if (string.IsNullOrEmpty(rating.UserId) || string.IsNullOrEmpty(rating.ItemId))
                {
                    throw new ArgumentException("Rating must have valid user and item IDs");
                }

                if (rating.Score < 1 || rating.Score > 5)
                {
                    throw new ArgumentException("Rating score must be between 1 and 5");
                }

                // Set creation date if not set
                if (rating.CreatedAt == default)
                {
                    rating.CreatedAt = DateTime.UtcNow;
                }

                // Check if item exists
                var item = await _itemRepository.GetByIdAsync(rating.ItemId);
                if (item == null)
                {
                    _logger.LogWarning($"Cannot add rating: Item {rating.ItemId} not found");
                    return false;
                }

                // Convert to Firebase model
                var firebaseRating = FirebaseRating.FromRating(rating);

                // Create multi-path update including indexes
                var updates = new Dictionary<string, object>
                {
                    [$"{RatingsPath}/{firebaseRating.Id}"] = firebaseRating.ToFirebaseObject()
                };

                // Add all index entries
                foreach (var entry in firebaseRating.CreateIndexEntries())
                {
                    updates[entry.Key] = entry.Value;
                }

                // Add the statistics updates
                var statsUpdates = await firebaseRating.CreateStatsUpdateEntriesAsync(_dataStore);
                foreach (var entry in statsUpdates)
                {
                    updates[entry.Key] = entry.Value;
                }

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding rating for user {rating?.UserId} and item {rating?.ItemId}");
                return false;
            }
        }

        /// <summary>
        /// Update an existing rating
        /// </summary>
        public async Task<bool> UpdateAsync(Rating rating)
        {
            try
            {
                // Validate rating
                if (rating == null)
                {
                    throw new ArgumentNullException(nameof(rating));
                }

                if (string.IsNullOrEmpty(rating.UserId) || string.IsNullOrEmpty(rating.ItemId ))
                {
                    throw new ArgumentException("Rating must have valid user and item IDs");
                }

                // Generate rating ID if not set
                // Fixed the type mismatch by converting the integer `rating.Id` to a string
                string ratingId = string.IsNullOrEmpty(rating.Id.ToString())
                    ? $"{rating.ItemId}_{rating.UserId}"
                    : rating.Id.ToString();


                // Check if the rating exists
                var existingRating = await GetByIdAsync(ratingId);
                if (existingRating == null)
                {
                    _logger.LogWarning($"Cannot update rating: Rating {ratingId} not found");
                    return false;
                }


                // Preserve creation date if not set
                if (rating.CreatedAt == default)
                {
                    rating.CreatedAt = existingRating.CreatedAt;
                }

                // Convert to Firebase model
                // Explicitly convert item/user IDs to strings if the method expects a string
                var firebaseRating = FirebaseRating.FromRating(rating, ratingId.ToString());

                // Create multi-path update including indexes
                var updates = new Dictionary<string, object>
                {
                    [$"{RatingsPath}/{firebaseRating.Id}"] = firebaseRating.ToFirebaseObject()
                };

                // Add all index entries
                foreach (var entry in firebaseRating.CreateIndexEntries())
                {
                    updates[entry.Key] = entry.Value;
                }

                // Add the statistics updates
                var statsUpdates = await firebaseRating.CreateStatsUpdateEntriesAsync(_dataStore);
                foreach (var entry in statsUpdates)
                {
                    updates[entry.Key] = entry.Value;
                }

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rating {rating?.Id}");
                return false;
            }
        }

        /// <summary>
        /// Delete a rating
        /// </summary>
        public async Task<bool> DeleteAsync(string ratingId)
        {
            try
            {
                // Get the rating to check if it exists and to get the item ID
                var rating = await GetByIdAsync(ratingId);
                if (rating == null)
                {
                    _logger.LogWarning($"Cannot delete rating: Rating {ratingId} not found");
                    return false;
                }

                // Convert to Firebase model to get index entries
                var firebaseRating = FirebaseRating.FromRating(rating, ratingId);

                // Create a set of paths to delete
                var updates = new Dictionary<string, object>
                {
                    [$"{RatingsPath}/{ratingId}"] = null // Set to null to delete
                };

                // Delete all index entries
                foreach (var entry in firebaseRating.CreateIndexEntries())
                {
                    updates[entry.Key] = null;
                }

                // Recalculate and update item statistics
                // Convert the integer ItemId to a string for the method that expects a string
                string itemIdStr = rating.ItemId.ToString();
                var statsUpdates = await UpdateItemRatingStatsAsync(rating.ItemId);
                foreach (var entry in statsUpdates)
                {
                    updates[entry.Key] = entry.Value;
                }

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting rating {ratingId}");
                return false;
            }
        }

        /// <summary>
        /// Delete a rating by user and item IDs
        /// </summary>
        public async Task<bool> DeleteByUserAndItemAsync(int userId, string itemId)
        {
            try
            {
                string ratingId = $"{itemId}_{userId}";
                return await DeleteAsync(ratingId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting rating for user {userId} and item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Get the average rating for an item
        /// </summary>
        public async Task<double?> GetAverageRatingAsync(string itemId)
        {
            try
            {
                // Get ratings for the item
                var ratings = await GetByItemIdAsync(itemId);
                if (!ratings.Any())
                {
                    return null;
                }

                // Calculate average
                return ratings.Average(r => r.Score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting average rating for item {itemId}");
                return null;
            }
        }

        /// <summary>
        /// Get the rating count for an item
        /// </summary>
        public async Task<int> GetRatingCountAsync(string itemId)
        {
            try
            {
                // Use the denormalized item_ratings index to count ratings
                var ratingEntries = await _dataStore.GetCollectionAsync<object>($"item_ratings/{itemId}");
                return ratingEntries?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting rating count for item {itemId}");
                return 0;
            }
        }

        /// <summary>
        /// Update item rating statistics
        /// </summary>
        public async Task<Dictionary<string, object>> UpdateItemRatingStatsAsync(string itemId)
        {
            try
            {
                // Get the average rating and count
                double? averageRating = await GetAverageRatingAsync(itemId);
                int ratingCount = await GetRatingCountAsync(itemId);

                // Create updates for the item's rating statistics
                var updates = new Dictionary<string, object>
                {
                    [$"{ItemsPath}/{itemId}/averageRating"] = averageRating,
                    [$"{ItemsPath}/{itemId}/ratingCount"] = ratingCount
                };

                return updates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rating statistics for item {itemId}");
                return new Dictionary<string, object>();
            }
        }

        // Implementation of IRatingsRepository.UpdateItemRatingStatsAsync
        async Task<bool> IRatingsRepository.UpdateItemRatingStatsAsync(string itemId)
        {
            try
            {
                var updates = await UpdateItemRatingStatsAsync(itemId);

                if (updates.Any())
                {
                    await _dataStore.BatchUpdateAsync(updates);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rating statistics for item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Adds a rating atomically with statistics update
        /// </summary>
        public async Task<bool> AddAtomicAsync(Rating rating)
        {
            int retryCount = 3;

            while (retryCount > 0)
            {
                try
                {
                    using var transaction = await _dataStore.BeginTransactionAsync();

                    // Check for existing rating
                    var ratingId = $"{rating.ItemId}_{rating.UserId}";
                    var existingRating = await transaction.GetEntityAsync<FirebaseRating>($"{RatingsPath}/{ratingId}");

                    if (existingRating != null)
                    {
                        throw new InvalidOperationException("Rating already exists");
                    }

                    // Get the item
                    var item = await transaction.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{rating.ItemId}");
                    if (item == null)
                    {
                        throw new InvalidOperationException($"Item {rating.ItemId} not found");
                    }

                    // Add rating
                    var firebaseRating = FirebaseRating.FromRating(rating);
                    await transaction.SetEntityAsync($"{RatingsPath}/{ratingId}", firebaseRating);

                    // Update rating indexes
                    await transaction.SetEntityAsync($"user_ratings/{rating.UserId}/{rating.ItemId}", true);
                    await transaction.SetEntityAsync($"item_ratings/{rating.ItemId}/{rating.UserId}", true);

                    // Update item statistics
                    var domainItem = item.ToItem();
                    domainItem.RatingCount += 1;

                    if (domainItem.RatingCount == 1)
                    {
                        domainItem.AverageRating = rating.Score;
                    }
                    else
                    {
                        domainItem.AverageRating =
                            ((domainItem.AverageRating ?? 0) * (domainItem.RatingCount - 1) + rating.Score) / domainItem.RatingCount;
                    }

                    domainItem.Version += 1;
                    domainItem.LastModified = DateTime.UtcNow;

                    var updatedItem = FirebaseItem.FromItem(domainItem);
                    // Fixed to use proper overload - removed the third argument or use proper type
                    await transaction.SetEntityAsync($"{ItemsPath}/{rating.ItemId}", updatedItem);
                    // Alternative fix if Version is intended to be used for optimistic concurrency:
                    // await transaction.SetEntityAsync($"{ItemsPath}/{rating.ItemId}", updatedItem, item.Version.ToString());

                    await transaction.CommitAsync();
                    return true;
                }
                catch (ConcurrencyException) when (retryCount > 1)
                {
                    retryCount--;
                    await Task.Delay(100 * (4 - retryCount)); // Exponential backoff
                    continue;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error adding rating for item {rating.ItemId}");
                    return false;
                }
            }

            return false;
        }
    }
}