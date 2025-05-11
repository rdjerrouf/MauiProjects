using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for handling user favorites
    /// </summary>
    public interface IFavoritesRepository
    {
        /// <summary>
        /// Add an item to a user's favorites
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> AddAsync(string userId, string itemId);

        /// <summary>
        /// Remove an item from a user's favorites
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> RemoveAsync(string userId, string itemId);

        /// <summary>
        /// Get a user's favorite item IDs
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of item IDs</returns>
        Task<List<string>> GetByUserIdAsync(string userId);

        /// <summary>
        /// Check if an item is in a user's favorites
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if the item is in the user's favorites</returns>
        Task<bool> IsItemFavoritedAsync(string userId, string itemId);

        /// <summary>
        /// Get detailed items for a user's favorites
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of favorite items</returns>
        Task<List<Item>> GetFavoriteItemsAsync(string userId);

        /// <summary>
        /// Get the users who have favorited an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>List of user IDs</returns>
        Task<List<string>> GetUsersByItemIdAsync(string itemId);

        /// <summary>
        /// Create a user favorite entry
        /// </summary>
        /// <param name="favorite">User favorite object</param>
        /// <returns>True if successful</returns>
        Task<bool> CreateAsync(UserFavorite favorite);
    }
}