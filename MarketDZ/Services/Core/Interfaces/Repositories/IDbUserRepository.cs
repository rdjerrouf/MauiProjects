using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for user database operations
    /// </summary>
    public interface IDbUserRepository
    {
        /// <summary>
        /// Gets a user by ID
        /// </summary>
        Task<User?> GetByIdAsync(string userId);

        /// <summary>
        /// Gets a user by email
        /// </summary>
        Task<User?> GetByEmailAsync(string email);

        /// <summary>
        /// Creates a new user
        /// </summary>
        Task<bool> CreateAsync(User user);

        /// <summary>
        /// Updates an existing user
        /// </summary>
        Task<bool> UpdateAsync(User user);

        /// <summary>
        /// Gets a verification token by its token string
        /// </summary>
        Task<VerificationToken?> GetVerificationTokenAsync(string token);

        /// <summary>
        /// Generic method to save an entity to a collection
        /// </summary>
        Task<bool> SaveEntityAsync<T>(string collectionName, T entity);

        /// <summary>
        /// Generic method to update an entity in a collection
        /// </summary>
        Task<bool> UpdateEntityAsync<T>(string collectionName, string id, T entity);
    }
}