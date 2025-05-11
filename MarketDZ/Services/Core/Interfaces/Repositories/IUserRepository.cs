using System.Collections.Generic;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities; // Assuming User and VerificationToken are here

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Canonical repository interface for user-related operations
    /// </summary>
    public interface IUserRepository
    {
        // --- Basic CRUD Operations ---

        /// <summary>
        /// Gets a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The ID of the user to retrieve.</param>
        /// <returns>A Task representing the asynchronous operation, containing the User object or null if not found.</returns>
        Task<User?> GetByIdAsync(string userId);

        /// <summary>
        /// Gets a user by their email address.
        /// </summary>
        /// <param name="email">The email address of the user to retrieve.</param>
        /// <returns>A Task representing the asynchronous operation, containing the User object or null if not found.</returns>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>
        /// Creates a new user in the repository.
        /// </summary>
        /// <param name="user">The User object to create.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if creation was successful, otherwise false.</returns>
        Task<bool> CreateAsync(User user);

        /// <summary>
        /// Updates an existing user in the repository.
        /// </summary>
        /// <param name="user">The User object with updated information.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if the update was successful, otherwise false.</returns>
        Task<bool> UpdateAsync(User user);

        /// <summary>
        /// Deletes a user from the repository by their ID.
        /// </summary>
        /// <param name="userId">The ID of the user to delete.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if deletion was successful, otherwise false.</returns>
        Task<bool> DeleteAsync(string userId);

        /// <summary>
        /// Retrieves all users from the repository.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation, containing a list of all User objects.</returns>
        Task<IEnumerable<User>> GetAllAsync(); // Or Task<IEnumerable<User>> if preferred

        // --- Verification Token Operations ---

        /// <summary>
        /// Gets a verification token by its token string.
        /// </summary>
        /// <param name="token">The unique token string.</param>
        /// <returns>A Task representing the asynchronous operation, containing the VerificationToken object or null if not found.</returns>
        Task<VerificationToken?> GetVerificationTokenAsync(string token);

        /// <summary>
        /// Creates a new verification token.
        /// </summary>
        /// <param name="token">The VerificationToken object to create.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if creation was successful, otherwise false.</returns>
        Task<bool> CreateVerificationTokenAsync(VerificationToken token);

        /// <summary>
        /// Deletes a verification token by its token string.
        /// </summary>
        /// <param name="token">The unique token string to delete.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if deletion was successful, otherwise false.</returns>
        Task<bool> DeleteVerificationTokenAsync(string token);

        // --- Favorites Management ---

        /// <summary>
        /// Adds an item to a user's list of favorites.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="itemId">The ID of the item to add to favorites.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if adding was successful, otherwise false.</returns>
        Task<bool> AddToFavoritesAsync(string userId, string itemId);

        /// <summary>
        /// Removes an item from a user's list of favorites.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="itemId">The ID of the item to remove from favorites.</param>
        /// <returns>A Task representing the asynchronous operation, containing true if removal was successful, otherwise false.</returns>
        Task<bool> RemoveFromFavoritesAsync(string userId, string itemId);

        /// <summary>
        /// Gets the list of item IDs favorited by a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A Task representing the asynchronous operation, containing a list of favorite item IDs.</returns>
        Task<List<string>> GetFavoritesAsync(string userId);
    }
}