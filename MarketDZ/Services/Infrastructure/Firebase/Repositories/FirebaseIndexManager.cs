using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase
{
    /// <summary>
    /// Manages Firebase indexes for efficient querying
    /// </summary>
    public class FirebaseIndexManager : IFirebaseIndexManager
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseIndexManager> _logger;

        public FirebaseIndexManager(
            IAppCoreDataStore dataStore,
            ILogger<FirebaseIndexManager> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Updates indexes for an entity
        /// </summary>
        public async Task UpdateEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            if (entity == null || indexCreator == null)
            {
                return;
            }

            try
            {
                // Create indexes for the entity
                var indexes = indexCreator(entity);

                if (indexes == null || !indexes.Any())
                {
                    return;
                }

                // Batch update all indexes
                await _dataStore.BatchUpdateAsync(indexes);

                _logger.LogDebug($"Updated {indexes.Count} indexes for entity at path: {entityPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating indexes for entity at path: {entityPath}");
                throw;
            }
        }

        /// <summary>
        /// Removes indexes for an entity
        /// </summary>
        public async Task RemoveEntityIndexesAsync<T>(string entityPath, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            if (entity == null || indexCreator == null)
            {
                return;
            }

            try
            {
                // Create indexes for the entity (to get the paths)
                var indexes = indexCreator(entity);

                if (indexes == null || !indexes.Any())
                {
                    return;
                }

                // Convert to deletion entries (set values to null)
                var deletions = indexes.Keys.ToDictionary(key => key, key => (object)null);

                // Batch delete all indexes
                await _dataStore.BatchUpdateAsync(deletions);

                _logger.LogDebug($"Removed {deletions.Count} indexes for entity at path: {entityPath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing indexes for entity at path: {entityPath}");
                throw;
            }
        }

        /// <summary>
        /// Validates indexes for an entity
        /// </summary>
        public async Task<bool> ValidateIndexesAsync<T>(string entityType, string entityId, T entity, Func<T, Dictionary<string, object>> indexCreator)
        {
            if (entity == null || indexCreator == null)
            {
                return false;
            }

            try
            {
                // Create expected indexes
                var expectedIndexes = indexCreator(entity);

                if (expectedIndexes == null || !expectedIndexes.Any())
                {
                    return true; // No indexes expected
                }

                // Check each index exists
                foreach (var index in expectedIndexes)
                {
                    var indexValue = await _dataStore.GetEntityAsync<object>(index.Key);

                    if (indexValue == null)
                    {
                        _logger.LogWarning($"Missing index for {entityType} {entityId} at path: {index.Key}");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating indexes for {entityType} {entityId}");
                return false;
            }
        }

        /// <summary>
        /// Repairs indexes for a collection
        /// </summary>
        public async Task RepairIndexesAsync<T>(string collectionPath, Func<T, string> idExtractor, Func<T, Dictionary<string, object>> indexCreator)
        {
            if (idExtractor == null || indexCreator == null)
            {
                return;
            }

            try
            {
                _logger.LogInformation($"Starting index repair for collection: {collectionPath}");

                // Get all entities in the collection
                var entities = await _dataStore.GetCollectionAsync<T>(collectionPath);

                if (entities == null || !entities.Any())
                {
                    _logger.LogInformation($"No entities found in collection: {collectionPath}");
                    return;
                }

                int repaired = 0;
                int failed = 0;

                foreach (var entity in entities)
                {
                    try
                    {
                        var entityId = idExtractor(entity);
                        var entityPath = $"{collectionPath}/{entityId}";

                        // Remove existing indexes
                        await RemoveEntityIndexesAsync(entityPath, entity, indexCreator);

                        // Create new indexes
                        await UpdateEntityIndexesAsync(entityPath, entity, indexCreator);

                        repaired++;
                    }
                    catch (Exception entityEx)
                    {
                        _logger.LogError(entityEx, $"Error repairing indexes for entity in {collectionPath}");
                        failed++;
                    }
                }

                _logger.LogInformation($"Index repair completed for {collectionPath}. Repaired: {repaired}, Failed: {failed}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error repairing indexes for collection: {collectionPath}");
                throw;
            }
        }

        /// <summary>
        /// Counts index entries in a path
        /// </summary>
        public async Task<int> CountIndexEntriesAsync(string indexRootPath)
        {
            try
            {
                return await _dataStore.GetCollectionSizeAsync(indexRootPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error counting index entries at path: {indexRootPath}");
                return 0;
            }
        }

        /// <summary>
        /// Cleans up orphaned indexes
        /// </summary>
        public async Task CleanupOrphanedIndexesAsync<T>(string collectionPath, string indexRootPath, Func<T, string> idExtractor)
        {
            if (idExtractor == null)
            {
                return;
            }

            try
            {
                _logger.LogInformation($"Starting orphaned index cleanup for {indexRootPath}");

                // Get all entities in the collection
                var entities = await _dataStore.GetCollectionAsync<T>(collectionPath);
                var entityIds = new HashSet<string>();

                if (entities != null)
                {
                    foreach (var entity in entities)
                    {
                        entityIds.Add(idExtractor(entity));
                    }
                }

                // Get all index entries
                var indexEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>(indexRootPath);

                if (indexEntries == null || !indexEntries.Any())
                {
                    _logger.LogInformation($"No index entries found at: {indexRootPath}");
                    return;
                }

                int cleaned = 0;
                var deletions = new Dictionary<string, object>();

                foreach (var entry in indexEntries)
                {
                    // Extract entity ID from the index entry
                    if (entry.TryGetValue("entityId", out var entityIdObj) ||
                        entry.TryGetValue("id", out entityIdObj) ||
                        entry.TryGetValue("targetId", out entityIdObj))
                    {
                        var entityId = entityIdObj?.ToString();

                        if (!string.IsNullOrEmpty(entityId) && !entityIds.Contains(entityId))
                        {
                            // This is an orphaned index entry
                            if (entry.TryGetValue("_path", out var pathObj))
                            {
                                var path = pathObj?.ToString();
                                if (!string.IsNullOrEmpty(path))
                                {
                                    deletions[path] = null;
                                    cleaned++;
                                }
                            }
                        }
                    }
                }

                if (deletions.Any())
                {
                    await _dataStore.BatchUpdateAsync(deletions);
                    _logger.LogInformation($"Cleaned {cleaned} orphaned index entries from {indexRootPath}");
                }
                else
                {
                    _logger.LogInformation($"No orphaned index entries found at {indexRootPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cleaning orphaned indexes for {indexRootPath}");
                throw;
            }
        }
    }
}