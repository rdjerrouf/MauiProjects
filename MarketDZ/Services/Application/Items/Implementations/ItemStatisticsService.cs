using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Items.Implementations
{
    /// <summary>
    /// Handles item statistics, ratings, favorites, views, and inquiries.
    /// </summary>
    public class ItemStatisticsService : IItemStatisticsService
    {
        private readonly IFavoritesRepository _favoritesRepository;
        private readonly IRatingsRepository _ratingsRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IUserRepository _userRepository;
        private readonly IItemCoreService _itemCoreService; // Needed for GetUserProfileStatisticsAsync
        private readonly ILogger<ItemStatisticsService> _logger;

        public ItemStatisticsService(
            IFavoritesRepository favoritesRepository,
            IRatingsRepository ratingsRepository,
            IItemRepository itemRepository,
            IUserRepository userRepository,
            IItemCoreService itemCoreService, // Inject core service
            ILogger<ItemStatisticsService> logger)
        {
            _favoritesRepository = favoritesRepository ?? throw new ArgumentNullException(nameof(favoritesRepository));
            _ratingsRepository = ratingsRepository ?? throw new ArgumentNullException(nameof(ratingsRepository));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _itemCoreService = itemCoreService ?? throw new ArgumentNullException(nameof(itemCoreService)); // Initialize
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Favorites and Ratings

        /// <summary>
        /// Add a favorite. Verifies user and item existence.
        /// </summary>
        public async Task<bool> AddFavoriteAsync(string userId, string itemId)
        {
            try
            {
                _logger.LogInformation($"Adding item {itemId} to favorites for user {userId}"); //
                // Verify item exists
                var item = await _itemRepository.GetByIdAsync(itemId); //
                if (item == null)
                {
                    _logger.LogError($"Cannot add favorite: Item {itemId} not found"); //
                    return false; //
                }

                // Verify user exists
                var user = await _userRepository.GetByIdAsync(userId); //
                if (user == null)
                {
                    _logger.LogError($"Cannot add favorite: User {userId} not found"); //
                    return false; //
                }

                // Add to favorites
                return await _favoritesRepository.AddAsync(userId, itemId); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding favorite for user {userId}, item {itemId}: {ex.Message}"); //
                return false; //
            }
        }

        /// <summary>
        /// Remove a favorite.
        /// </summary>
        public async Task<bool> RemoveFavoriteAsync(string userId, string itemId)
        {
            try
            {
                _logger.LogInformation($"Removing item {itemId} from favorites for user {userId}"); //
                return await _favoritesRepository.RemoveAsync(userId, itemId); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing favorite for user {userId}, item {itemId}: {ex.Message}"); //
                return false; //
            }
        }

        /// <summary>
        /// Get user's favorite items.
        /// </summary>
        public async Task<ObservableCollection<Item>> GetUserFavoriteItemsAsync(string userId)
        {
            try
            {
                var items = await _favoritesRepository.GetFavoriteItemsAsync(userId); //
                return new ObservableCollection<Item>(items); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving favorite items for user {userId}: {ex.Message}"); //
                return new ObservableCollection<Item>(); //
            }
        }

        /// <summary>
        /// checking if item is favorited by user
        /// </summary>
        public async Task<bool> IsItemFavoritedAsync(string userId, string itemId)
        {
            try
            {
                return await _favoritesRepository.IsItemFavoritedAsync(userId, itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking favorite status for user {userId}, item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Add a rating.
        /// </summary>
        public async Task<bool> AddRatingAsync(string userId, string itemId, int score, string review)
        {
            try
            {
                var rating = new Rating
                {
                    UserId = userId, //
                    ItemId = itemId, //
                    Score = score, //
                    Review = review, //
                    CreatedAt = DateTime.UtcNow //
                };
                return await _ratingsRepository.AddAsync(rating); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding rating by user {userId} for item {itemId}: {ex.Message}"); //
                return false; //
            }
        }

        /// <summary>
        /// Get user ratings (ratings submitted by the user).
        /// </summary>
        public async Task<IEnumerable<Rating>> GetUserRatingsAsync(string userId)
        {
            try
            {
                // Convert userId from string to int before passing it to the repository method
                if (!int.TryParse(userId, out int userIdAsInt))
                {
                    _logger.LogError($"Invalid userId format: {userId}");
                    return Enumerable.Empty<Rating>();
                }

                return await _ratingsRepository.GetByUserIdAsync(userIdAsInt); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings submitted by user {userId}: {ex.Message}"); //
                return Enumerable.Empty<Rating>(); //
            }
        }

        /// <summary>
        /// Get item ratings (ratings submitted for the item).
        /// </summary>
        public async Task<IEnumerable<Rating>> GetItemRatingsAsync(string itemId)
        {
            try
            {
                return await _ratingsRepository.GetByItemIdAsync(itemId); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings for item {itemId}: {ex.Message}"); //
                return Enumerable.Empty<Rating>(); //
            }
        }

        #endregion

        #region Statistics and Metrics

        /// <summary>
        /// Get user profile statistics. Uses IItemCoreService to get user items.
        /// </summary>
        public async Task<UserProfileStatistics> GetUserProfileStatisticsAsync(string userId)
        {
            try
            {
                _logger.LogInformation($"Getting profile statistics for user {userId}"); //
                var user = await _userRepository.GetByIdAsync(userId); //
                if (user == null)
                {
                    _logger.LogError($"User {userId} not found for profile statistics."); //
                    return new UserProfileStatistics { UserId = userId }; // Return default/empty stats
                }

                // Use IItemCoreService to get the user's items
                var userItems = await _itemCoreService.GetUserItemsAsync(userId); // Depends on IItemCoreService

                // Cast userItems to the appropriate type to access the 'Status' property
                int activeItemsCount = userItems.OfType<Item>().Count(i => i.Status == ItemStatus.Active); //
                                                                                                           // Fix for the CS1061 error in the GetUserProfileStatisticsAsync method
                int totalViews = userItems.OfType<Item>().Sum(i => i.ViewCount); //
                                                                                 // Fix for CS1061: Ensure that the 'userItems' collection is properly cast to the expected type (e.g., List<Item>).
                                                                                 // Update the code in the GetUserProfileStatisticsAsync method as follows:

                double averageRating = userItems.OfType<Item>().Any(i => i.RatingCount > 0)
                    ? userItems.OfType<Item>().Where(i => i.RatingCount > 0).Average(i => i.AverageRating ?? 0) // Ensure null safety
                    : 0;

                // Get recent ratings using the method within this service
                var ratings = await GetUserItemRatingsAsync(userId); // Calls internal method
                var recentRatings = ratings.OrderByDescending(r => r.CreatedAt).Take(5).ToList(); //

                var statistics = new UserProfileStatistics
                {
                    UserId = userId,
                    TotalListings = userItems.OfType<Item>().Count(), //
                    ActiveListings = activeItemsCount, //
                    TotalViews = totalViews, //
                    AverageRating = averageRating, //
                    JoinedDate = user.CreatedAt, //
                    RecentRatings = recentRatings //
                };
                _logger.LogInformation($"Retrieved profile statistics for user {userId}"); //
                return statistics; //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user profile statistics for user {userId}: {ex.Message}"); //
                return new UserProfileStatistics { UserId = userId }; // Return default/empty stats on error
            }
        }

        /// <summary>
        /// Get ratings for items owned by a user.
        /// </summary>
        public async Task<IEnumerable<Rating>> GetUserItemRatingsAsync(string userId)
        {
            // This fetches ratings submitted FOR items owned by the user, not ratings submitted BY the user.
            try
            {
                // Convert userId from string to int before passing it to the repository method
                if (!int.TryParse(userId, out int userIdAsInt))
                {
                    _logger.LogError($"Invalid userId format: {userId}");
                    return Enumerable.Empty<Rating>();
                }

                return await _ratingsRepository.GetForUserItemsAsync(userIdAsInt); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting ratings for items owned by user {userId}: {ex.Message}"); //
                return Enumerable.Empty<Rating>(); //
            }
        }

        /// <summary>
        /// Get top performing items based on view count.
        /// </summary>
        public async Task<IEnumerable<ItemPerformanceDto>> GetTopPerformingItemsAsync(int count)
        {
            try
            {
                // Get all items - consider if this is efficient for large datasets.
                // Might need a dedicated repository method if performance is critical.
                var items = await _itemRepository.GetAllAsync(); //
                return items
                    .OrderByDescending(i => i.ViewCount) //
                    .Take(count) //
                    .Select(i => new ItemPerformanceDto //
                    {
                        ItemId = i.Id, //
                        Title = i.Title, //
                        Category = i.Category.ToString(), // Convert ItemCategory enum to string
                        ViewCount = i.ViewCount, //
                        InquiryCount = i.InquiryCount //
                    })
                    .ToList(); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting top {count} performing items: {ex.Message}"); //
                return Enumerable.Empty<ItemPerformanceDto>(); //
            }
        }

        /// <summary>
        /// Get item statistics including ratings.
        /// </summary>
        public async Task<ItemStatistics?> GetItemStatisticsAsync(string itemId)
        {
            try
            {
                var itemStats = await _itemRepository.GetStatisticsAsync(itemId); //
                if (itemStats == null)
                {
                    _logger.LogWarning($"Statistics not found for item {itemId}.");
                    return null; //
                }

                // Add the ratings to the statistics DTO/Model
                itemStats.Ratings = (await _ratingsRepository.GetByItemIdAsync(itemId)).ToList(); // Fetch ratings for the item
                return itemStats; //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting item statistics for item {itemId}: {ex.Message}"); //
                return null; //
            }
        }

        /// <summary>
        /// Increment item view count.
        /// </summary>
        public async Task<bool> IncrementItemViewAsync(string itemId)
        {
            try
            {
                return await _itemRepository.IncrementViewCountAsync(itemId); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error incrementing item view count for item {itemId}: {ex.Message}"); //
                return false; //
            }
        }

        /// <summary>
        /// Record an item inquiry (increment inquiry count).
        /// </summary>
        public async Task<bool> RecordItemInquiryAsync(string itemId)
        {
            try
            {
                return await _itemRepository.IncrementInquiryCountAsync(itemId); //
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording item inquiry for item {itemId}: {ex.Message}"); //
                return false; //
            }
        }

        // Note: UpdateItemStatusAsync and IsItemAvailableAsync were moved to ItemCoreService
        // as they relate more to the item's core state than pure statistics.

        #endregion
    }
}