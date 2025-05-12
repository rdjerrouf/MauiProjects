using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using LiteDB;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase-specific implementation of the User repository
    /// </summary>
    public class FirebaseUserRepository : BaseRepository<User, FirebaseUser>, IUserRepository
    {
        private readonly IFirebaseTransactionHelper _transactionHelper;
        private readonly IFirebaseIndexManager _indexManager;

        public FirebaseUserRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<User, FirebaseUser> entityMapper,
            ILogger<FirebaseUserRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper,
            IFirebaseIndexManager indexManager)
            : base(dataStore, entityMapper, logger, cacheService, "users")
        {
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
            _indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
        }

        /// <summary>
        /// Gets a user by email address
        /// </summary>
        public async Task<User> GetByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                Logger.LogWarning("Cannot get user with null or empty email");
                return null;
            }

            string cacheKey = $"user_email_{email.ToLowerInvariant()}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<User>(cacheKey, out var cachedUser))
            {
                return cachedUser;
            }

            try
            {
                // Normalize email for lookup (Firebase's indexing rules)
                var normalizedEmail = email.ToLowerInvariant().Replace(".", "_dot_").Replace("@", "_at_");

                // Get user ID from email index
                var userId = await DataStore.GetEntityAsync<string>($"users_by_email/{normalizedEmail}");

                if (string.IsNullOrEmpty(userId))
                {
                    Logger.LogDebug($"No user found with email {email}");
                    return null;
                }

                // Get user by ID
                var user = await GetByIdAsync(userId);

                // Cache the result
                CacheService?.AddToCache(cacheKey, user, TimeSpan.FromMinutes(30));

                return user;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting user by email {email}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new user with proper indexing
        /// </summary>
        public override async Task<string> CreateAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // Set default values if needed
            if (user.CreatedAt == default)
            {
                user.CreatedAt = DateTime.UtcNow;
            }

            try
            {
                // Generate ID if needed
                if (string.IsNullOrEmpty(user.Id))
                {
                    user.Id = Guid.NewGuid().ToString();
                }

                // Convert to Firebase entity
                var firebaseUser = EntityMapper.ToEntity(user);

                // Create or update in a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Save main entity
                await transaction.SetEntityAsync($"{CollectionPath}/{user.Id}", firebaseUser);

                // Create indexes
                var indexUpdates = ((FirebaseUser)firebaseUser).CreateIndexEntries();
                foreach (var update in indexUpdates)
                {
                    await transaction.SetEntityAsync(update.Key, update.Value);
                }

                await transaction.CommitAsync();

                // Update cache
                string userCacheKey = $"{CollectionPath}_{user.Id}";
                CacheService?.AddToCache(userCacheKey, user, DefaultCacheDuration);

                string emailCacheKey = $"user_email_{user.Email.ToLowerInvariant()}";
                CacheService?.AddToCache(emailCacheKey, user, DefaultCacheDuration);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return user.Id;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error creating user: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing user and maintains indexes
        /// </summary>
        public override async Task<bool> UpdateAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            if (string.IsNullOrEmpty(user.Id))
            {
                Logger.LogWarning("Cannot update user with null or empty ID");
                return false;
            }

            try
            {
                // Get existing user to compare for index changes
                var existingEntity = await DataStore.GetEntityAsync<FirebaseUser>($"{CollectionPath}/{user.Id}");
                if (existingEntity == null)
                {
                    Logger.LogWarning($"User with ID {user.Id} not found for update");
                    return false;
                }

                // Check if email changed (requires index update)
                bool emailChanged = !string.Equals(existingEntity.Email, user.Email, StringComparison.OrdinalIgnoreCase);

                // Convert domain to entity
                var updatedEntity = EntityMapper.ToEntity(user);

                // Use a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Remove old email index if changed
                if (emailChanged)
                {
                    var oldNormalizedEmail = existingEntity.Email.ToLowerInvariant().Replace(".", "_dot_").Replace("@", "_at_");
                    await transaction.DeleteEntityAsync($"users_by_email/{oldNormalizedEmail}");
                }

                // Save updated entity
                await transaction.SetEntityAsync($"{CollectionPath}/{user.Id}", updatedEntity);

                // Create new email index if changed
                if (emailChanged)
                {
                    var newNormalizedEmail = user.Email.ToLowerInvariant().Replace(".", "_dot_").Replace("@", "_at_");
                    await transaction.SetEntityAsync($"users_by_email/{newNormalizedEmail}", user.Id);
                }

                await transaction.CommitAsync();

                // Update cache
                string userCacheKey = $"{CollectionPath}_{user.Id}";
                CacheService?.InvalidateCache(userCacheKey);
                CacheService?.AddToCache(userCacheKey, user, DefaultCacheDuration);

                // Update email cache if changed
                if (emailChanged)
                {
                    string oldEmailCacheKey = $"user_email_{existingEntity.Email.ToLowerInvariant()}";
                    CacheService?.InvalidateCache(oldEmailCacheKey);

                    string newEmailCacheKey = $"user_email_{user.Email.ToLowerInvariant()}";
                    CacheService?.AddToCache(newEmailCacheKey, user, DefaultCacheDuration);
                }

                // Invalidate collection cache
                InvalidateCollectionCache();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating user with ID {user.Id}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a verification token by token string
        /// </summary>
        public async Task<VerificationToken> GetVerificationTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogWarning("Cannot get verification token with null or empty value");
                return null;
            }

            string cacheKey = $"verification_token_{token}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<VerificationToken>(cacheKey, out var cachedToken))
            {
                return cachedToken;
            }

            try
            {
                // Tokens are stored with their token value as the key
                var tokenEntity = await DataStore.GetEntityAsync<Dictionary<string, object>>($"verification_tokens/{token}");

                if (tokenEntity == null || !tokenEntity.Any())
                {
                    return null;
                }

                // Convert to VerificationToken object
                var verificationToken = new VerificationToken
                {
                    Id = token,
                    Token = token,
                    UserId = tokenEntity.ContainsKey("userId") ? tokenEntity["userId"]?.ToString() : null,
                    Type = tokenEntity.ContainsKey("type") && Enum.TryParse<VerificationType>(tokenEntity["type"]?.ToString(), out var type)
                        ? type
                        : VerificationType.EmailVerification,
                    CreatedAt = tokenEntity.ContainsKey("createdAt") && DateTime.TryParse(tokenEntity["createdAt"]?.ToString(), out var createdAt)
                        ? createdAt
                        : DateTime.UtcNow,
                    ExpiresAt = tokenEntity.ContainsKey("expiresAt") && DateTime.TryParse(tokenEntity["expiresAt"]?.ToString(), out var expiresAt)
                        ? expiresAt
                        : DateTime.UtcNow.AddDays(1),
                    IsUsed = tokenEntity.ContainsKey("isUsed") && bool.TryParse(tokenEntity["isUsed"]?.ToString(), out var isUsed) && isUsed
                };

                // Fetch the user also
                if (!string.IsNullOrEmpty(verificationToken.UserId))
                {
                    verificationToken.User = await GetByIdAsync(verificationToken.UserId);
                }

                // Cache the result (short duration for security)
                CacheService?.AddToCache(cacheKey, verificationToken, TimeSpan.FromMinutes(5));

                return verificationToken;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting verification token {token}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a verification token
        /// </summary>
        public async Task<bool> CreateVerificationTokenAsync(VerificationToken token)
        {
            if (token == null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (string.IsNullOrEmpty(token.Token))
            {
                Logger.LogWarning("Cannot create verification token with null or empty token value");
                return false;
            }

            try
            {
                // Generate ID if needed
                if (string.IsNullOrEmpty(token.Id))
                {
                    token.Id = token.Token; // Use token as ID
                }

                // Set default values
                if (token.CreatedAt == default)
                {
                    token.CreatedAt = DateTime.UtcNow;
                }

                if (token.ExpiresAt == default)
                {
                    // Default expiration based on type
                    token.ExpiresAt = token.Type switch
                    {
                        VerificationType.EmailVerification => DateTime.UtcNow.AddDays(7),
                        VerificationType.PasswordReset => DateTime.UtcNow.AddHours(24),
                        _ => DateTime.UtcNow.AddDays(1)
                    };
                }

                // Create token entity
                var tokenEntity = new Dictionary<string, object>
                {
                    ["id"] = token.Id,
                    ["token"] = token.Token,
                    ["userId"] = token.UserId,
                    ["type"] = token.Type.ToString(),
                    ["createdAt"] = token.CreatedAt,
                    ["expiresAt"] = token.ExpiresAt,
                    ["isUsed"] = token.IsUsed
                };

                // Save token
                await DataStore.SetEntityAsync($"verification_tokens/{token.Token}", tokenEntity);

                // Create index by user ID for easy lookup
                if (!string.IsNullOrEmpty(token.UserId))
                {
                    await DataStore.SetEntityAsync($"user_verification_tokens/{token.UserId}/{token.Token}", tokenEntity);
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error creating verification token: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Marks a verification token as used
        /// </summary>
        public async Task<bool> MarkTokenAsUsedAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogWarning("Cannot mark null or empty token as used");
                return false;
            }

            try
            {
                // Get current token
                var tokenEntity = await DataStore.GetEntityAsync<Dictionary<string, object>>($"verification_tokens/{token}");

                if (tokenEntity == null || !tokenEntity.Any())
                {
                    Logger.LogWarning($"Token {token} not found for marking as used");
                    return false;
                }

                // Update the isUsed flag
                tokenEntity["isUsed"] = true;

                // Save token
                await DataStore.SetEntityAsync($"verification_tokens/{token}", tokenEntity);

                // Update user index if available
                if (tokenEntity.ContainsKey("userId") && tokenEntity["userId"] != null)
                {
                    string userId = tokenEntity["userId"].ToString();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        await DataStore.SetEntityAsync($"user_verification_tokens/{userId}/{token}", tokenEntity);
                    }
                }

                // Invalidate cache
                string cacheKey = $"verification_token_{token}";
                CacheService?.InvalidateCache(cacheKey);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error marking token {token} as used: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a verification token
        /// </summary>
        public async Task<bool> DeleteVerificationTokenAsync(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Logger.LogWarning("Cannot delete null or empty token");
                return false;
            }

            try
            {
                // Get token to find userId for index
                var tokenEntity = await DataStore.GetEntityAsync<Dictionary<string, object>>($"verification_tokens/{token}");
                string userId = null;

                if (tokenEntity != null && tokenEntity.ContainsKey("userId"))
                {
                    userId = tokenEntity["userId"]?.ToString();
                }

                // Use a transaction to ensure consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Delete the main token
                await transaction.DeleteEntityAsync($"verification_tokens/{token}");

                // Delete the user index if available
                if (!string.IsNullOrEmpty(userId))
                {
                    await transaction.DeleteEntityAsync($"user_verification_tokens/{userId}/{token}");
                }

                await transaction.CommitAsync();

                // Invalidate cache
                string cacheKey = $"verification_token_{token}";
                CacheService?.InvalidateCache(cacheKey);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error deleting token {token}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Adds an item to a user's favorites
        /// </summary>
        public async Task<bool> AddToFavoritesAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId))
            {
                Logger.LogWarning($"Cannot add to favorites with null or empty userId: {userId} or itemId: {itemId}");
                return false;
            }

            try
            {
                // Create a timestamp for ordering
                var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Save to user favorites in a transaction
                using var transaction = await DataStore.BeginTransactionAsync();

                // Save to user favorites
                await transaction.SetEntityAsync($"user_favorites/{userId}/{itemId}", timestamp);

                // Save to item favorites (for reverse lookup)
                await transaction.SetEntityAsync($"item_favorites/{itemId}/{userId}", timestamp);

                // Increment the item's favorite count
                var item = await transaction.GetEntityAsync<Dictionary<string, object>>($"items/{itemId}");
                if (item != null)
                {
                    int favoriteCount = 0;
                    if (item.ContainsKey("favoriteCount") && item["favoriteCount"] != null)
                    {
                        int.TryParse(item["favoriteCount"].ToString(), out favoriteCount);
                    }

                    item["favoriteCount"] = favoriteCount + 1;
                    await transaction.SetEntityAsync($"items/{itemId}", item);
                }

                await transaction.CommitAsync();

                // Invalidate caches
                string favoritesCacheKey = $"user_{userId}_favorites";
                CacheService?.InvalidateCache(favoritesCacheKey);

                string itemCacheKey = $"items_{itemId}";
                CacheService?.InvalidateCache(itemCacheKey);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error adding item {itemId} to favorites for user {userId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Removes an item from a user's favorites
        /// </summary>
        public async Task<bool> RemoveFromFavoritesAsync(string userId, string itemId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId))
            {
                Logger.LogWarning($"Cannot remove from favorites with null or empty userId: {userId} or itemId: {itemId}");
                return false;
            }

            try
            {
                // Use a transaction for consistency
                using var transaction = await DataStore.BeginTransactionAsync();

                // Check if it's actually in favorites first
                var favoriteExists = await transaction.GetEntityAsync<object>($"user_favorites/{userId}/{itemId}");
                if (favoriteExists == null)
                {
                    // Not in favorites, nothing to do
                    return true;
                }

                // Remove from user favorites
                await transaction.DeleteEntityAsync($"user_favorites/{userId}/{itemId}");

                // Remove from item favorites
                await transaction.DeleteEntityAsync($"item_favorites/{itemId}/{userId}");

                // Decrement the item's favorite count
                var item = await transaction.GetEntityAsync<Dictionary<string, object>>($"items/{itemId}");
                if (item != null)
                {
                    int favoriteCount = 0;
                    if (item.ContainsKey("favoriteCount") && item["favoriteCount"] != null)
                    {
                        int.TryParse(item["favoriteCount"].ToString(), out favoriteCount);
                    }

                    item["favoriteCount"] = Math.Max(0, favoriteCount - 1); // Ensure not negative
                    await transaction.SetEntityAsync($"items/{itemId}", item);
                }

                await transaction.CommitAsync();

                // Invalidate caches
                string favoritesCacheKey = $"user_{userId}_favorites";
                CacheService?.InvalidateCache(favoritesCacheKey);

                string itemCacheKey = $"items_{itemId}";
                CacheService?.InvalidateCache(itemCacheKey);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error removing item {itemId} from favorites for user {userId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a user's favorite item IDs
        /// </summary>
        public async Task<List<string>> GetFavoritesAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                Logger.LogWarning("Cannot get favorites for null or empty user ID");
                return new List<string>();
            }

            string cacheKey = $"user_{userId}_favorites";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<string>>(cacheKey, out var cachedFavorites))
            {
                return cachedFavorites;
            }

            try
            {
                // Get favorite entries
                var favoritesPath = $"user_favorites/{userId}";
                var favorites = await DataStore.GetCollectionAsync<Dictionary<string, object>>(favoritesPath);

                if (favorites == null || !favorites.Any())
                {
                    return new List<string>();
                }

                // Extract item IDs (keys in the dictionary)
                var itemIds = favorites.Select(f => f.Keys.FirstOrDefault()).Where(id => !string.IsNullOrEmpty(id)).ToList();

                // Cache the result
                CacheService?.AddToCache(cacheKey, itemIds, TimeSpan.FromMinutes(10));

                return itemIds;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting favorites for user {userId}: {ex.Message}");
                return new List<string>();
            }
        }

        /// <summary>
        /// Generic method to save an entity to a collection
        /// </summary>
        public async Task<bool> SaveEntityAsync<T>(string collectionName, T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (string.IsNullOrEmpty(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
            }

            try
            {
                // Extract ID if entity has an Id property
                string id = null;
                var idProperty = typeof(T).GetProperty("Id");
                if (idProperty != null)
                {
                    id = idProperty.GetValue(entity)?.ToString();
                }

                // Generate ID if needed
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();

                    // Set ID if possible
                    if (idProperty != null && idProperty.CanWrite)
                    {
                        idProperty.SetValue(entity, id);
                    }
                }

                // Save entity
                await DataStore.SetEntityAsync($"{collectionName}/{id}", entity);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error saving entity to collection {collectionName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Generic method to update an entity in a collection
        /// </summary>
        public async Task<bool> UpdateEntityAsync<T>(string collectionName, string id, T entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (string.IsNullOrEmpty(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("ID cannot be null or empty", nameof(id));
            }

            try
            {
                // Set ID if entity has an Id property
                var idProperty = typeof(T).GetProperty("Id");
                if (idProperty != null && idProperty.CanWrite)
                {
                    idProperty.SetValue(entity, id);
                }

                // Update entity
                await DataStore.SetEntityAsync($"{collectionName}/{id}", entity);

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating entity with ID {id} in collection {collectionName}: {ex.Message}");
                return false;
            }
        }
    }
}