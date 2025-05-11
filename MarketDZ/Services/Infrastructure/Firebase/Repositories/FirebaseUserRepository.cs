using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Enhanced Firebase implementation of IDbUserRepository with optimized email indexing
    /// </summary>
    public class FirebaseUserRepository : IDbUserRepository, IUserRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseUserRepository> _logger;
        private const string UsersPath = "users";
        private const string EmailIndexPath = "users_by_email";
        private const string VerificationTokensPath = "verificationTokens";

        public FirebaseUserRepository(
                IAppCoreDataStore dataStore,
                ILogger<FirebaseUserRepository> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Helper Methods

        /// <summary>
        /// Normalizes an email for consistent indexing
        /// </summary>
        private string NormalizeEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            return email.ToLowerInvariant()
                .Replace(".", "_dot_")
                .Replace("@", "_at_");
        }

        #endregion

        #region IDbUserRepository Implementation

        /// <summary>
        /// Get a user by their ID
        /// </summary>
        public async Task<User> GetByIdAsync(string userId)
        {
            try
            {
                string path = $"{UsersPath}/{userId}";
                _logger.LogDebug($"Fetching user by ID from path: {path}");

                var firebaseUser = await _dataStore.GetEntityAsync<FirebaseUser>(path);
                return firebaseUser?.ToUser();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by ID {userId}");
                return null;
            }
        }

        /// <summary>
        /// Get a user by their email address with optimized index lookup
        /// </summary>
        public async Task<User> GetByEmailAsync(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                _logger.LogWarning("Attempted to get user with empty email");
                return null;
            }

            try
            {
                // Normalize the email for consistent indexing
                string normalizedEmail = NormalizeEmail(email);
                string indexPath = $"{EmailIndexPath}/{normalizedEmail}";

                _logger.LogDebug($"Looking up user ID with normalized email path: {indexPath}");

                // Direct index lookup to get the user ID
                var userId = await _dataStore.GetEntityAsync<string>(indexPath);

                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogInformation($"No user found for email: {email}");
                    return null;
                }

                // Fetch user details using the ID
                _logger.LogDebug($"Fetching user details for ID: {userId}");
                return await GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by email {email}");
                return null;
            }
        }

        /// <summary>
        /// Create a new user with atomic email index update
        /// </summary>
        public async Task<bool> CreateAsync(User user)
        {
            try
            {
                // Validate user data
                if (user == null) throw new ArgumentNullException(nameof(user));
                if (string.IsNullOrWhiteSpace(user.Email)) throw new ArgumentException("User email cannot be empty");

                // Check if email already exists
                var existingUser = await GetByEmailAsync(user.Email);
                if (existingUser != null)
                {
                    _logger.LogWarning($"User with email {user.Email} already exists");
                    return false;
                }

                // Set defaults
                if (user.CreatedAt == default)
                {
                    user.CreatedAt = DateTime.UtcNow;
                }

                // Convert to Firebase model
                var firebaseUser = FirebaseUser.FromUser(user);

                // Normalize email for indexing
                string normalizedEmail = NormalizeEmail(user.Email);

                // Create multi-path update including indexes
                var updates = new Dictionary<string, object>
                {
                    // Main user record
                    [$"{UsersPath}/{firebaseUser.Id}"] = firebaseUser.ToFirebaseObject(),

                    // Email index
                    [$"{EmailIndexPath}/{normalizedEmail}"] = firebaseUser.Id
                };

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                _logger.LogInformation($"Created user with ID {firebaseUser.Id} and email {user.Email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating user {user?.Email}");
                return false;
            }
        }

        /// <summary>
        /// Update an existing user with support for email change
        /// </summary>
        public async Task<bool> UpdateAsync(User user)
        {
            try
            {
                // Validate user data
                if (user == null) throw new ArgumentNullException(nameof(user));
                if (string.IsNullOrEmpty(user.Id)) throw new ArgumentException("User ID must be valid");

                // Get existing user to check for email change and preserve creation timestamp
                var existingFirebaseUser = await _dataStore.GetEntityAsync<FirebaseUser>($"{UsersPath}/{user.Id}");
                if (existingFirebaseUser == null)
                {
                    _logger.LogWarning($"User with ID {user.Id} not found for update");
                    return false;
                }

                var existingUser = existingFirebaseUser.ToUser();

                // Check if email has changed
                bool emailChanged = !string.Equals(
                    existingUser.Email?.Trim().ToLowerInvariant(),
                    user.Email?.Trim().ToLowerInvariant(),
                    StringComparison.OrdinalIgnoreCase
                );

                // Convert to Firebase model, preserving creation timestamp
                var firebaseUser = FirebaseUser.FromUser(user, existingFirebaseUser.Id);
                firebaseUser.CreatedTimestamp = existingFirebaseUser.CreatedTimestamp;

                // Prepare updates
                var updates = new Dictionary<string, object>
                {
                    // Update main user record
                    [$"{UsersPath}/{firebaseUser.Id}"] = firebaseUser.ToFirebaseObject()
                };

                // Handle email index if changed
                if (emailChanged)
                {
                    // Remove old email index
                    string oldNormalizedEmail = NormalizeEmail(existingUser.Email!);
                    updates[$"{EmailIndexPath}/{oldNormalizedEmail}"] = null;

                    // Add new email index
                    string newNormalizedEmail = NormalizeEmail(user.Email!);
                    updates[$"{EmailIndexPath}/{newNormalizedEmail}"] = firebaseUser.Id;

                    _logger.LogInformation($"Email updated for user {user.Id}: {existingUser.Email} -> {user.Email}");
                }

                // Execute atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating user {user?.Id}");
                return false;
            }
        }

        /// <summary>
        /// Gets a verification token
        /// </summary>
        public async Task<VerificationToken> GetVerificationTokenAsync(string token)
        {
            try
            {
                // Note: You may want to create a FirebaseVerificationToken adapter
                // similar to other adapter classes
                return await _dataStore.GetEntityAsync<VerificationToken>($"{VerificationTokensPath}/{token}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting verification token {token}");
                return null;
            }
        }

        /// <summary>
        /// Implements the generic SaveEntityAsync method required by IDbUserRepository
        /// </summary>
        public async Task<bool> SaveEntityAsync<T>(string collectionName, T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                if (string.IsNullOrEmpty(collectionName))
                    throw new ArgumentException("Collection name cannot be empty", nameof(collectionName));

                // Special handling for VerificationToken
                if (entity is VerificationToken token && collectionName == "verification_tokens")
                {
                    return await CreateVerificationTokenAsync(token);
                }

                // For other entity types, use the data store directly
                string path = $"{collectionName}/{Guid.NewGuid()}";
                await _dataStore.SetEntityAsync(path, entity);

                _logger.LogInformation($"Saved entity to path: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving entity to collection: {collectionName}");
                return false;
            }
        }

        /// <summary>
        /// Implements the generic UpdateEntityAsync method required by IDbUserRepository
        /// </summary>
        public async Task<bool> UpdateEntityAsync<T>(string collectionName, string id, T entity)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                if (string.IsNullOrEmpty(collectionName))
                    throw new ArgumentException("Collection name cannot be empty", nameof(collectionName));

                if (string.IsNullOrEmpty(id))
                    throw new ArgumentException("ID cannot be empty", nameof(id));

                // Special handling for VerificationToken
                if (entity is VerificationToken token && collectionName == "verification_tokens")
                {
                    // For tokens, we just overwrite the existing token
                    return await CreateVerificationTokenAsync(token);
                }

                // For other entity types, use the data store directly
                string path = $"{collectionName}/{id}";
                await _dataStore.SetEntityAsync(path, entity);

                _logger.LogInformation($"Updated entity at path: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating entity at {collectionName}/{id}");
                return false;
            }
        }

        #endregion

        #region IUserRepository Implementation

        /// <summary>
        /// Get user by ID - compatibility method for IUserRepository (handles int to string conversion)
        /// </summary>
        async Task<User> IUserRepository.GetByIdAsync(string userId)
        {
            return await GetByIdAsync(userId.ToString());
        }

        /// <summary>
        /// Delete a user - implementation for both interfaces
        /// </summary>
        public async Task<bool> DeleteAsync(string userId)
        {
            try
            {
                // Get the user to get email for index removal
                var firebaseUser = await _dataStore.GetEntityAsync<FirebaseUser>($"{UsersPath}/{userId}");
                if (firebaseUser == null)
                {
                    _logger.LogWarning($"User with ID {userId} not found for deletion");
                    return false;
                }

                // Create a set of paths to delete
                var updates = new Dictionary<string, object>
                {
                    [$"{UsersPath}/{userId}"] = null // Set to null to delete
                };

                // Remove from email index
                string normalizedEmail = firebaseUser.Email.ToLowerInvariant().Replace(".", "_dot_").Replace("@", "_at_");
                updates[$"users_by_email/{normalizedEmail}"] = null;

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Delete a user - compatibility method for IUserRepository (handles int to string conversion)
        /// </summary>
        async Task<bool> IUserRepository.DeleteAsync(string userId)
        {
            return await DeleteAsync(userId.ToString());
        }

        /// <summary>
        /// Get all users
        /// </summary>
        public async Task<IEnumerable<User>> GetAllAsync()
        {
            try
            {
                var firebaseUsers = await _dataStore.GetCollectionAsync<FirebaseUser>(UsersPath);

                return firebaseUsers
                    .Where(fu => fu != null)
                    .Select(fu => fu.ToUser())
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return Enumerable.Empty<User>();
            }
        }

        /// <summary>
        /// Create a verification token
        /// </summary>
        public async Task<bool> CreateVerificationTokenAsync(VerificationToken token)
        {
            try
            {
                if (token == null || string.IsNullOrEmpty(token.Token))
                    throw new ArgumentException("Token must have a valid token value");

                await _dataStore.SetEntityAsync($"{VerificationTokensPath}/{token.Token}", token);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating verification token");
                return false;
            }
        }

        /// <summary>
        /// Delete a verification token
        /// </summary>
        public async Task<bool> DeleteVerificationTokenAsync(string token)
        {
            try
            {
                await _dataStore.DeleteEntityAsync($"{VerificationTokensPath}/{token}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting verification token {token}");
                return false;
            }
        }

        /// <summary>
        /// Add an item to user's favorites
        /// </summary>
        public async Task<bool> AddToFavoritesAsync(string userId, string itemId)
        {
            try
            {
                // Use the favorites repository pattern instead
                var firebaseFavorite = new FirebaseFavorite
                {
                    UserId = userId,
                    ItemId = itemId,
                    DateAddedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };

                // Create multi-path update for user favorites
                var updates = new Dictionary<string, object>
                {
                    [$"user_favorites/{userId}/{itemId}"] = firebaseFavorite.DateAddedTimestamp,
                    [$"item_favorites/{itemId}/{userId}"] = firebaseFavorite.DateAddedTimestamp
                };

                // Also update favorite count on the item
                updates[$"items/{itemId}/favoriteCount"] = new Dictionary<string, object> { { ".sv", "increment" }, { "value", 1 } };
                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                _logger.LogInformation($"Added item {itemId} to favorites for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding item {itemId} to favorites for user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Add an item to user's favorites - compatibility method for IUserRepository (handles int to string conversion)
        /// </summary>
        async Task<bool> IUserRepository.AddToFavoritesAsync(string userId, string itemId)
        {
            return await AddToFavoritesAsync(userId.ToString(), itemId);
        }

        /// <summary>
        /// Remove an item from user's favorites
        /// </summary>
        public async Task<bool> RemoveFromFavoritesAsync(string userId, string itemId)
        {
            try
            {
                // Create multi-path update to remove from favorites
                var updates = new Dictionary<string, object>
                {
                    [$"user_favorites/{userId}/{itemId}"] = null, // Set to null to delete
                    [$"item_favorites/{itemId}/{userId}"] = null  // Set to null to delete
                };

                // Also update favorite count on the item (decrement) - use AtomicIncrementAsync
                await DataStoreExtensions.AtomicIncrementAsync(_dataStore, $"items/{itemId}", "favoriteCount", -1);

                // Explicitly specify the generic type for BatchUpdateAsync to resolve ambiguity
                await _dataStore.BatchUpdateAsync(updates);

                _logger.LogInformation($"Removed item {itemId} from favorites for user {userId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing item {itemId} from favorites for user {userId}");
                return false;
            }
        }

        /// <summary>
        /// Remove an item from user's favorites - compatibility method for IUserRepository (handles int to string conversion)
        /// </summary>
        async Task<bool> IUserRepository.RemoveFromFavoritesAsync(string userId, string itemId)
        {
            return await RemoveFromFavoritesAsync(userId.ToString(), itemId);
        }

        /// <summary>
        /// Get a user's favorite items
        /// </summary>
        public async Task<List<string>> GetFavoritesAsync(string userId)
        {
            try
            {
                // Use the denormalized user_favorites index
                var favorites = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"user_favorites/{userId}");

                if (favorites == null || !favorites.Any())
                    return new List<string>();

                // Extract item IDs directly as strings
                return favorites
                    .Select(f => f.Keys.FirstOrDefault()) // Get the key of each dictionary entry
                    .Where(static k => !string.IsNullOrEmpty(k))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting favorites for user {userId}");
                return new List<string>();
            }
        }
        /// <summary>
        /// Get a user's favorite items - compatibility method for IUserRepository (handles int to string conversion)
        /// </summary>
        async Task<List<string>> IUserRepository.GetFavoritesAsync(string userId)
        {
            return await GetFavoritesAsync(userId.ToString());
        }

        #endregion
    }
}