using MarketDZ.Models;
using MarketDZ.Services.DbServices;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Extensions for IDbUserRepository to add missing methods
    /// </summary>
    public static class DbUserRepositoryExtensions
    {
        /// <summary>
        /// Extension method for creating verification tokens
        /// </summary>
        public static async Task<bool> CreateVerificationTokenAsync(this IDbUserRepository repository, VerificationToken token)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            // Implementation for saving a verification token
            return await repository.SaveEntityAsync("verification_tokens", token);
        }

        /// <summary>
        /// Extension method for updating verification tokens
        /// </summary>
        public static async Task<bool> UpdateVerificationTokenAsync(this IDbUserRepository repository, VerificationToken token)
        {
            if (repository == null)
            {
                throw new ArgumentNullException(nameof(repository));
            }

            if (string.IsNullOrEmpty(token.Id))
            {
                throw new ArgumentException("Token ID cannot be null or empty", nameof(token));
            }

            // Implementation for updating a verification token
            return await repository.UpdateEntityAsync("verification_tokens", token.Id, token);
        }
    }
}