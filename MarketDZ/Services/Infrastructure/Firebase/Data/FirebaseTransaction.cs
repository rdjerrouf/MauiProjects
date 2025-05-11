using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Data
{
    /// <summary>
    /// Firebase implementation of ITransaction
    /// </summary>
    public class FirebaseTransaction : ITransaction
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly ILogger<FirebaseTransaction> _logger;
        private readonly Dictionary<string, object> _operations = new Dictionary<string, object>();
        private readonly HashSet<string> _deletions = new HashSet<string>();
        private readonly Dictionary<string, int> _expectedVersions = new Dictionary<string, int>();
        private bool _isCommitted = false;
        private bool _isRolledBack = false;
        private bool _isDisposed = false;
        private FirebaseDataStore firebaseDataStore;
        private ILogger<FirebaseDataStore> logger;
        private readonly IAppCoreDataStore _dataStore;
        /// <summary>
        /// Creates a new Firebase transaction
        /// </summary>
        /// <param name="firebaseClient">Firebase client</param>
        public FirebaseTransaction(
                 FirebaseClient firebaseClient,
                 IAppCoreDataStore dataStore,
                 ILogger<FirebaseTransaction> logger)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public FirebaseTransaction(FirebaseClient firebaseClient, FirebaseDataStore firebaseDataStore, ILogger<FirebaseDataStore> logger)
        {
            _firebaseClient = firebaseClient;
            this.firebaseDataStore = firebaseDataStore;
            this.logger = logger;
        }

        /// <summary>
        /// Retrieves an entity within the transaction
        /// </summary>
        public async Task<T> GetEntityAsync<T>(string path)
        {
            ThrowIfInvalidState();

            // Check if we're planning to modify this entity
            if (_operations.TryGetValue(path, out var value) && value is T typedValue)
            {
                return typedValue;
            }

            // Check if we're planning to delete this entity
            if (_deletions.Contains(path))
            {
                return default;
            }

            // If not in our transaction memory, get it from Firebase
            return await _firebaseClient.Child(path).OnceSingleAsync<T>();
        }

        /// <summary>
        /// Sets an entity with version checking
        /// </summary>
        public Task SetEntityAsync<T>(string path, T data, int? expectedVersion = null)
        {
            ThrowIfInvalidState();

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Cannot set null data in transaction");
            }

            _operations[path] = data;
            _deletions.Remove(path);

            if (expectedVersion.HasValue)
            {
                _expectedVersions[path] = expectedVersion.Value;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates specific fields of an entity within the transaction
        /// </summary>
        public async Task UpdateFieldsAsync(string path, IDictionary<string, object> updates)
        {
            ThrowIfInvalidState();

            if (updates == null || updates.Count == 0)
            {
                throw new ArgumentException("Updates cannot be null or empty", nameof(updates));
            }

            // Get current entity state (either from our memory or from Firebase)
            dynamic currentEntity;
            if (_operations.TryGetValue(path, out var value))
            {
                currentEntity = value;
            }
            else if (_deletions.Contains(path))
            {
                throw new InvalidOperationException($"Cannot update fields of entity at {path} because it is marked for deletion");
            }
            else
            {
                currentEntity = await _firebaseClient.Child(path).OnceSingleAsync<object>();
                if (currentEntity == null)
                {
                    currentEntity = new Dictionary<string, object>();
                }
            }

            // Apply updates
            var entityType = currentEntity.GetType();
            foreach (var update in updates)
            {
                try
                {
                    var prop = entityType.GetProperty(update.Key);
                    if (prop != null && prop.CanWrite)
                    {
                        prop.SetValue(currentEntity, update.Value);
                    }
                    else
                    {
                        // Handle dynamic objects or dictionaries
                        if (currentEntity is IDictionary<string, object> dict)
                        {
                            dict[update.Key] = update.Value;
                        }
                    }
                }
                catch (Exception)
                {
                    // Continue with other properties if one fails
                }
            }

            // Store updated entity
            _operations[path] = currentEntity;
        }

        /// <summary>
        /// Deletes an entity within the transaction
        /// </summary>
        public Task DeleteEntityAsync(string path)
        {
            ThrowIfInvalidState();

            // Remove from operations if it exists
            _operations.Remove(path);

            // Mark for deletion
            _deletions.Add(path);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Commits all changes with concurrency checks
        /// </summary>
        public async Task CommitAsync()
        {
            ThrowIfInvalidState();

            try
            {
                // First, validate all version preconditions
                if (_expectedVersions.Any())
                {
                    foreach (var (path, expectedVersion) in _expectedVersions)
                    {
                        var currentEntity = await _dataStore.GetEntityAsync<Dictionary<string, object>>(path);

                        if (currentEntity == null)
                        {
                            throw new ConcurrencyException($"Entity not found at {path} during concurrency check");
                        }

                        if (currentEntity.TryGetValue("version", out var versionObj) &&
                            int.TryParse(versionObj.ToString(), out int currentVersion))
                        {
                            if (currentVersion != expectedVersion)
                            {
                                throw new ConcurrencyException($"Concurrency conflict at {path}: expected version {expectedVersion}, found {currentVersion}");
                            }
                        }
                    }
                }

                // Build atomic update dictionary
                var updateDictionary = new Dictionary<string, object>();

                // Add all updates with version increments
                foreach (var operation in _operations)
                {
                    var path = operation.Key;
                    var data = operation.Value;

                    if (data is IVersionedEntity versionedEntity)
                    {
                        versionedEntity.Version += 1;
                        versionedEntity.LastModified = DateTime.UtcNow;
                    }

                    updateDictionary[path] = data;
                }

                // Add deletion entries
                foreach (var path in _deletions)
                {
                    updateDictionary[path] = null;
                }

                // Execute atomic update
                await _dataStore.BatchUpdateAsync(updateDictionary);

                _isCommitted = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Transaction commit failed");
                throw;
            }
        }

        /// <summary>
        /// Cancels all changes in the transaction
        /// </summary>
        public Task RollbackAsync()
        {
            ThrowIfInvalidState();

            // Just mark as rolled back and clear operations
            _operations.Clear();
            _deletions.Clear();
            _isRolledBack = true;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Disposes the transaction
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            if (!_isCommitted && !_isRolledBack)
            {
                // Auto-rollback uncommitted transactions
                try
                {
                    RollbackAsync().GetAwaiter().GetResult();
                }
                catch
                {
                    // Ignore errors during disposal
                }
            }

            _operations.Clear();
            _deletions.Clear();
            _isDisposed = true;
        }

        /// <summary>
        /// Validates the transaction state
        /// </summary>
        private void ThrowIfInvalidState()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FirebaseTransaction), "Transaction has been disposed");

            if (_isCommitted)
                throw new InvalidOperationException("Transaction has already been committed");

            if (_isRolledBack)
                throw new InvalidOperationException("Transaction has been rolled back");
        }

        public Task SetEntityAsync<T>(string path, T data)
        {
            throw new NotImplementedException();
        }


        // Supporting classes
        public interface IVersionedEntity
        {
            int Version { get; set; }
            DateTime LastModified { get; set; }
        }

        public class ConcurrencyException : Exception
        {
            public ConcurrencyException(string message) : base(message) { }
        }
    }
}