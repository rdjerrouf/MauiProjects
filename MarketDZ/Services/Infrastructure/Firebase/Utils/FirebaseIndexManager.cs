using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    /// <summary>
    /// Manages denormalized indexes for Firebase data
    /// </summary>
    public class FirebaseIndexManager : IFirebaseIndexManager
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger _logger;

        public FirebaseIndexManager(IAppCoreDataStore dataStore, ILogger<FirebaseIndexManager> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        /// <summary>
        /// Creates or updates all indexes for an entity
        /// </summary>
        public async Task UpdateEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                // Get index entries from the creator function
                var indexEntries = indexCreator(entity);

                if (indexEntries == null || !indexEntries.Any())
                    return;

                // Add the main entity path to ensure atomic update
                indexEntries[entityPath] = entity;

                // Execute batch update
                await _dataStore.BatchUpdateAsync(indexEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating indexes for entity at path {entityPath}");
                throw;
            }
        }

        /// <summary>
        /// Removes all indexes for an entity
        /// </summary>
        public async Task RemoveEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            try
            {
                if (entity == null)
                    throw new ArgumentNullException(nameof(entity));

                // Get index entries from the creator function
                var indexEntries = indexCreator(entity);

                if (indexEntries == null || !indexEntries.Any())
                    return;

                // Set all values to null to delete
                var deletionEntries = indexEntries.ToDictionary(
                    kvp => kvp.Key,
                    kvp => (object?)null); // Explicitly cast null to object? to match the expected type

                // Add the main entity path
                deletionEntries[entityPath] = null;

                // Execute batch update
                await _dataStore.BatchUpdateAsync(deletionEntries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)); // Use null-forgiving operator
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing indexes for entity at path {entityPath}");
                throw;
            }
        }

        /// <summary>
        /// Validates that indexes match the source data
        /// </summary>
        public async Task<bool> ValidateIndexesAsync<T>(string entityType, string entityId, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            try
            {
                // Get expected index entries
                var expectedIndexes = indexCreator(entity);

                // Check each index
                foreach (var index in expectedIndexes)
                {
                    var indexExists = await _dataStore.GetEntityAsync<object>(index.Key) != null;

                    if (!indexExists)
                    {
                        _logger.LogWarning($"Missing index: {index.Key} for {entityType}/{entityId}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating indexes for {entityType}/{entityId}");
                return false;
            }
        }

        /// <summary>
        /// Repairs all indexes for a collection of entities
        /// </summary>
        public async Task RepairIndexesAsync<T>(string collectionPath, Func<T, string> idExtractor, Func<T, Dictionary<string, object>> indexCreator)
        {
            try
            {
                // Get all entities
                var entities = await _dataStore.GetCollectionAsync<T>(collectionPath);

                if (entities == null || !entities.Any())
                    return;

                // Process each entity
                foreach (var entity in entities)
                {
                    try
                    {
                        string entityId = idExtractor(entity);
                        string entityPath = $"{collectionPath}/{entityId}";

                        // Check if indexes are valid
                        bool indexesValid = await ValidateIndexesAsync(collectionPath, entityId, entity, indexCreator);

                        // Repair if needed
                        if (!indexesValid)
                        {
                            _logger.LogInformation($"Repairing indexes for {collectionPath}/{entityId}");
                            await UpdateEntityIndexesAsync(entityPath, entity, indexCreator);
                        }
                    }
                    catch (Exception entityEx)
                    {
                        _logger.LogError(entityEx, $"Error processing entity in {collectionPath}");
                        // Continue with next entity
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error repairing indexes for collection {collectionPath}");
                throw;
            }
        }

        public async Task<int> CountIndexEntriesAsync(string indexRootPath)
        {
            try
            {
                var indexEntries = await _dataStore.GetCollectionAsync<object>(indexRootPath);
                var dict = indexEntries as IDictionary<string, object>;
                return dict?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting index entries at {indexRootPath}");
                return -1;
            }
        }
        /// <summary>
        /// Cleans up orphaned index entries that no longer reference valid entities
        /// </summary>
        public async Task CleanupOrphanedIndexesAsync<T>(string collectionPath, string indexRootPath, Func<T, string> idExtractor)
        {
            try
            {
                var indexEntries = await _dataStore.GetCollectionAsync<object>(indexRootPath);
                if (indexEntries == null || !indexEntries.Any())
                    return;

                // Cast to IDictionary<string, object> to access Key property
                if (indexEntries is not IDictionary<string, object> dict)
                    return;

                var entities = await _dataStore.GetCollectionAsync<T>(collectionPath);
                var validIds = new HashSet<string>(entities.Select(e => idExtractor(e)));

                var updates = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> indexEntry in dict)
                {
                    if (!validIds.Contains(indexEntry.Key))
                    {
                        updates[$"{indexRootPath}/{indexEntry.Key}"] = null!; // use null-forgiving operator
                        _logger.LogInformation($"Found orphaned index entry: {indexRootPath}/{indexEntry.Key}");
                    }
                }

                if (updates.Any())
                {
                    await _dataStore.BatchUpdateAsync(updates);
                    _logger.LogInformation($"Cleaned up {updates.Count} orphaned index entries");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cleaning up orphaned indexes at {indexRootPath}");
                throw;
            }
        }
        /// <summary>
        /// Creates indexes for a batch of entities efficiently
        /// </summary>
        public async Task BatchCreateIndexesAsync<T>(IEnumerable<T> entities, string collectionPath, Func<T, string> idExtractor, Func<T, Dictionary<string, object>> indexCreator)
        {
            try
            {
                var batchUpdates = new Dictionary<string, object>();

                foreach (var entity in entities)
                {
                    string entityId = idExtractor(entity);
                    string entityPath = $"{collectionPath}/{entityId}";

                    // Get index entries from the creator function
                    var indexEntries = indexCreator(entity);

                    if (indexEntries == null || !indexEntries.Any())
                        continue;

                    // Add the main entity path to ensure atomic update
                    indexEntries[entityPath] = entity;

                    // Add to batch
                    foreach (var entry in indexEntries)
                    {
                        batchUpdates[entry.Key] = entry.Value;
                    }
                }

                if (batchUpdates.Any())
                {
                    await _dataStore.BatchUpdateAsync(batchUpdates);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating batch indexes");
                throw;
            }
        }
    }
}
