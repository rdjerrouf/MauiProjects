using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemStatisticsService
    {
        /// <summary> Adds an item to a user's favorites list. </summary> //
        Task<bool> AddFavoriteAsync(string userId, string itemId);

        /// <summary> Removes an item from a user's favorites list. </summary> //
        Task<bool> RemoveFavoriteAsync(string userId, string itemId);

        /// <summary> Retrieves the list of items favorited by a user. </summary> //
        Task<ObservableCollection<Item>> GetUserFavoriteItemsAsync(string userId);

        /// <summary> Adds a rating (score and review) from a user for an item. </summary> //
        Task<bool> AddRatingAsync(string userId, string itemId, int score, string review);

        /// <summary> Retrieves all ratings submitted by a specific user. </summary> //
        Task<IEnumerable<Rating>> GetUserRatingsAsync(string userId);

        /// <summary> Retrieves all ratings submitted for a specific item. </summary> //
        Task<IEnumerable<Rating>> GetItemRatingsAsync(string itemId);

        /// <summary> Compiles and retrieves statistics for a user's profile. </summary> //
        Task<UserProfileStatistics> GetUserProfileStatisticsAsync(string userId);

        /// <summary> Retrieves ratings submitted for items owned by a specific user. </summary> //
        Task<IEnumerable<Rating>> GetUserItemRatingsAsync(string userId);

        /// <summary> Retrieves the top performing items based on metrics like views (count specifies how many). </summary> //
        Task<IEnumerable<ItemPerformanceDto>> GetTopPerformingItemsAsync(int count);

        /// <summary> Retrieves detailed statistics for a single item. </summary> //
        Task<ItemStatistics?> GetItemStatisticsAsync(string itemId);

        /// <summary> Increments the view count for an item. </summary> //
        Task<bool> IncrementItemViewAsync(string itemId);

        /// <summary> Records that an inquiry was made about an item (increments inquiry count). </summary> //
        Task<bool> RecordItemInquiryAsync(string itemId);

        /// <summary> Checking if item is favorite by user </summary> //
        Task<bool> IsItemFavoritedAsync(string userId, string itemId);
    }
}