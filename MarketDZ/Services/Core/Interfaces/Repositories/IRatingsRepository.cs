using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for handling ratings and reviews
    /// </summary>
    public interface IRatingsRepository
    {
        /// <summary>
        /// Get a rating by its ID
        /// </summary>
        /// <param name="ratingId">Rating ID</param>
        /// <returns>Rating or null if not found</returns>
        Task<Rating> GetByIdAsync(string ratingId);

        /// <summary>
        /// Get a rating by user and item IDs
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="itemId">Item ID</param>
        /// <returns>Rating or null if not found</returns>
        Task<Rating> GetByUserAndItemAsync(int userId, string itemId);

        /// <summary>
        /// Get all ratings by a user
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <returns>List of ratings by the user</returns>
        Task<List<Rating>> GetByUserIdAsync(int userId);

        /// <summary>
        /// Get all ratings for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>List of ratings for the item</returns>
        Task<List<Rating>> GetByItemIdAsync(string itemId);

        /// <summary>
        /// Get all ratings for items owned by a user
        /// </summary>
        /// <param name="userId">User ID (item owner)</param>
        /// <returns>List of ratings for the user's items</returns>
        Task<List<Rating>> GetForUserItemsAsync(int userId);

        /// <summary>
        /// Add a new rating
        /// </summary>
        /// <param name="rating">Rating to add</param>
        /// <returns>True if successful</returns>
        Task<bool> AddAsync(Rating rating);

        /// <summary>
        /// Update an existing rating
        /// </summary>
        /// <param name="rating">Rating to update</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateAsync(Rating rating);

        /// <summary>
        /// Delete a rating
        /// </summary>
        /// <param name="ratingId">Rating ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteAsync(string ratingId);

        /// <summary>
        /// Delete a rating by user and item IDs
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> DeleteByUserAndItemAsync(int userId, string itemId);

        /// <summary>
        /// Get the average rating for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Average rating or null if no ratings</returns>
        Task<double?> GetAverageRatingAsync(string itemId);

        /// <summary>
        /// Get the rating count for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>Number of ratings</returns>
        Task<int> GetRatingCountAsync(string itemId);

        /// <summary>
        /// Update item rating statistics
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>True if successful</returns>
        Task<bool> UpdateItemRatingStatsAsync(string itemId);
    }
}