using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase-specific implementation of the Ratings repository
    /// </summary>
    public class FirebaseRatingsRepository : BaseRepository<Rating, FirebaseRating>, IRatingsRepository
    {
        private readonly IFirebaseTransactionHelper _transactionHelper;

        public FirebaseRatingsRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<Rating, FirebaseRating> entityMapper,
            ILogger<FirebaseRatingsRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper)
            : base(dataStore, entityMapper, logger, cacheService, "ratings")
        {
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
        }

        /// <summary>
        /// Get a rating by user and item IDs
        /// </summary>
        public async Task<Rating> GetByUserAndItemAsync(int userId, string itemId)
        {
            // Create composite key for user-item rating
            var compositeKey = $"{userId}_{itemId}";

            string cacheKey = $"rating_{compositeKey}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<Rating>(cacheKey, out var cachedRating))
            {
                return cachedRating;
            }

            try
            {
                // Try to get by composite key first
                var rating = await GetByIdAsync(compositeKey);

                if (rating != null)
                {
                    CacheService?.AddToCache(cacheKey, rating, DefaultCacheDuration);
                    return rating;
                }

                // Fallback: search in user ratings
                var userRatings = await GetByUserIdAsync(userId);
                rating = userRatings.FirstOrDefault(r => r.ItemId == itemId);

                if (rating != null)
                {
                    CacheService?.AddToCache(cacheKey, rating, DefaultCacheDuration);
                }

                return rating;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting rating for user {userId} and item {itemId}");
                return null;
            }
        }

        /// <summary>
        /// Get all ratings by a user
        /// </summary>
        public async Task<List<Rating>> GetByUserIdAsync(int userId)
        {
            string cacheKey = $"user_{userId}_ratings";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<Rating>>(cacheKey, out var cachedRatings))
            {
                return cachedRatings;
            }

            try
            {
                // Get from user ratings index
                var userRatingsPath = $"user_ratings/{userId}";
                var ratingEntries = await DataStore.GetCollectionAsync<Dictionary<string, object>>(userRatingsPath);

                if (ratingEntries == null || !ratingEntries.Any())
                {
                    return new List<Rating>();
                }

                // Extract item IDs and fetch ratings
                var ratings = new List<Rating>();

                foreach (var entry in ratingEntries)
                {
                    if (entry.ContainsKey("itemId"))
                    {
                        var itemId = entry["itemId"]?.ToString();
                        var rating = await GetByUserAndItemAsync(userId, itemId);
                        if (rating != null)
                        {
                            ratings.Add(rating);
                        }
                    }
                }

                // Cache the result
                CacheService?.AddToCache(cacheKey, ratings, TimeSpan.FromMinutes(10));

                return ratings;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting ratings for user {userId}");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Get all ratings for an item
        /// </summary>
        public async Task<List<Rating>> GetByItemIdAsync(string itemId)
        {
            string cacheKey = $"item_{itemId}_ratings";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<Rating>>(cacheKey, out var cachedRatings))
            {
                return cachedRatings;
            }

            try
            {
                // Get from item ratings index
                var itemRatingsPath = $"item_ratings/{itemId}";
                var ratingEntries = await DataStore.GetCollectionAsync<Dictionary<string, object>>(itemRatingsPath);

                if (ratingEntries == null || !ratingEntries.Any())
                {
                    return new List<Rating>();
                }

                // Extract user IDs and fetch ratings
                var ratings = new List<Rating>();

                foreach (var entry in ratingEntries)
                {
                    if (entry.ContainsKey("userId"))
                    {
                        var userId = entry["userId"]?.ToString();
                        if (int.TryParse(userId, out var parsedUserId))
                        {
                            var rating = await GetByUserAndItemAsync(parsedUserId, itemId);
                            if (rating != null)
                            {
                                ratings.Add(rating);
                            }
                        }
                    }
                }

                // Cache the result
                CacheService?.AddToCache(cacheKey, ratings, TimeSpan.FromMinutes(10));

                return ratings;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting ratings for item {itemId}");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Get all ratings for items owned by a user
        /// </summary>
        public async Task<List<Rating>> GetForUserItemsAsync(int userId)
        {
            string cacheKey = $"user_{userId}_item_ratings";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<Rating>>(cacheKey, out var cachedRatings))
            {
                return cachedRatings;
            }

            try
            {
                // First, get all items owned by the user
                var userItemsPath = $"user_items/{userId}";
                var userItems = await DataStore.GetCollectionAsync<Dictionary<string, object>>(userItemsPath);

                if (userItems == null || !userItems.Any())
                {
                    return new List<Rating>();
                }

                // Then get ratings for each item
                var allRatings = new List<Rating>();

                foreach (var itemEntry in userItems)
                {
                    if (itemEntry.ContainsKey("itemId"))
                    {
                        var itemId = itemEntry["itemId"]?.ToString();
                        var itemRatings = await GetByItemIdAsync(itemId);
                        allRatings.AddRange(itemRatings);
                    }
                }

                // Cache the result
                CacheService?.AddToCache(cacheKey, allRatings, TimeSpan.FromMinutes(15));

                return allRatings;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting ratings for user {userId}'s items");
                return new List<Rating>();
            }
        }

        /// <summary>
        /// Add a new rating
        /// </summary>
        public async Task<bool> AddAsync(Rating rating)
        {
            if (rating == null)
            {
                throw new ArgumentNullException(nameof(rating));
            }

            try
            {
                // Set composite ID
                rating.Id = $"{rating.UserId}_{rating.ItemId}";

                // Create the rating
                var ratingId = await CreateAsync(rating);

                // Update item statistics
                await UpdateItemRatingStatsAsync(rating.ItemId);

                // Invalidate caches
                InvalidateRatingCaches(rating.UserId.ToString(), rating.ItemId);

                return !string.IsNullOrEmpty(ratingId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error adding rating for item {rating.ItemId} by user {rating.UserId}");
                return false;
            }
        }

        /// <summary>
        /// Update an existing rating
        /// </summary>
        public override async Task<bool> UpdateAsync(Rating rating)
        {
            if (rating == null)
            {
                throw new ArgumentNullException(nameof(rating));
            }

            try
            {
                // Ensure ID is set correctly
                if (string.IsNullOrEmpty(rating.Id))
                {
                    rating.Id = $"{rating.UserId}_{rating.ItemId}";
                }

                // Update the rating
                var result = await base.UpdateAsync(rating);

                if (result)
                {
                    // Update item statistics
                    await UpdateItemRatingStatsAsync(rating.ItemId);

                    // Invalidate caches
                    InvalidateRatingCaches(rating.UserId.ToString(), rating.ItemId);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating rating {rating.Id}");
                return false;
            }
        }

        /// <summary>
        /// Delete a rating by user and item IDs
        /// </summary>
        public async Task<bool> DeleteByUserAndItemAsync(int userId, string itemId)
        {
            var compositeKey = $"{userId}_{itemId}";

            try
            {
                // Delete by composite key
                var result = await DeleteAsync(compositeKey);

                if (result)
                {
                    // Update item statistics
                    await UpdateItemRatingStatsAsync(itemId);

                    // Invalidate caches
                    InvalidateRatingCaches(userId.ToString(), itemId);
                }

                return result;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error deleting rating for user {userId} and item {itemId}");
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
                var ratings = await GetByItemIdAsync(itemId);

                if (ratings == null || !ratings.Any())
                {
                    return null;
                }

                return ratings.Average(r => r.Score);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error calculating average rating for item {itemId}");
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
                var ratings = await GetByItemIdAsync(itemId);
                return ratings?.Count ?? 0;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting rating count for item {itemId}");
                return 0;
            }
        }

        /// <summary>
        /// Update item rating statistics
        /// </summary>
        public async Task<bool> UpdateItemRatingStatsAsync(string itemId)
        {
            try
            {
                // Get all ratings for the item
                var ratings = await GetByItemIdAsync(itemId);

                // Calculate statistics
                var count = ratings.Count;
                var average = count > 0 ? ratings.Average(r => r.Score) : 0;

                // Update item with new statistics
                var updates = new Dictionary<string, object>
                {
                    ["ratingCount"] = count,
                    ["averageRating"] = average
                };

                await DataStore.UpdateEntityFieldsAsync($"items/{itemId}", updates);

                // Invalidate item cache
                string itemCacheKey = $"items_{itemId}";
                CacheService?.InvalidateCache(itemCacheKey);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating rating statistics for item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Invalidates rating-related caches
        /// </summary>
        private void InvalidateRatingCaches(string userId, string itemId)
        {
            if (CacheService == null)
            {
                return;
            }

            // Invalidate specific rating cache
            var compositeKey = $"{userId}_{itemId}";
            CacheService.InvalidateCache($"rating_{compositeKey}");

            // Invalidate user ratings cache
            CacheService.InvalidateCache($"user_{userId}_ratings");

            // Invalidate item ratings cache
            CacheService.InvalidateCache($"item_{itemId}_ratings");

            // Invalidate user item ratings cache
            CacheService.InvalidateCache($"user_{userId}_item_ratings");

            // Invalidate item statistics cache
            CacheService.InvalidateCache($"item_stats_{itemId}");
        }
    }
}