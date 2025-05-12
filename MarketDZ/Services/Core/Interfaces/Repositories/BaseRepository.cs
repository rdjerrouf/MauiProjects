using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Infrastructure;
using Microsoft.Extensions.Logging;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Models;
using MarketDZ.Models.Infrastructure.Common;

namespace MarketDZ.Services.Core.Repositories
{
    /// <summary>
    /// Base generic repository implementation providing common CRUD operations across database providers
    /// </summary>
    public abstract class BaseRepository<TDomain, TEntity> where TDomain : class, IEntity where TEntity : class
    {
        protected readonly IAppCoreDataStore DataStore;
        protected readonly IEntityMapper<TDomain, TEntity> EntityMapper;
        protected readonly ILogger Logger;
        protected readonly string CollectionPath;
        protected readonly ICacheService CacheService;
        protected readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Constructor for BaseRepository
        /// </summary>
        /// <param name="dataStore">Data store implementation</param>
        /// <param name="entityMapper">Entity mapper for conversion between domain and data models</param>
        /// <param name="logger">Logger instance</param>
        /// <param name="cacheService">Optional cache service</param>
        /// <param name="collectionPath">Path to the entity collection in the data store</param>
        protected BaseRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<TDomain, TEntity> entityMapper,
            ILogger logger,
            ICacheService cacheService = null,
            string collectionPath = null)
        {
            DataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            EntityMapper = entityMapper ?? throw new ArgumentNullException(nameof(entityMapper));
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CacheService = cacheService; // Optional

            // If collection path not provided, derive from entity type name
            CollectionPath = collectionPath ?? typeof(TDomain).Name.ToLowerInvariant() + "s";
        }

        /// <summary>
        /// Gets an entity by ID with caching support
        /// </summary>
        public virtual async Task<TDomain> GetByIdAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Logger.LogWarning($"Attempted to get {typeof(TDomain).Name} with null or empty ID");
                return null;
            }

            string cacheKey = $"{CollectionPath}_{id}";

            // Try to get from cache first if available
            if (CacheService != null && CacheService.TryGetFromCache<TDomain>(cacheKey, out var cachedEntity))
            {
                Logger.LogDebug($"Cache hit for {typeof(TDomain).Name} with ID {id}");
                return cachedEntity;
            }

            try
            {
                var entityPath = $"{CollectionPath}/{id}";
                var entity = await DataStore.GetEntityAsync<TEntity>(entityPath);

                if (entity == null)
                {
                    Logger.LogDebug($"{typeof(TDomain).Name} with ID {id} not found");
                    return null;
                }

                var domainEntity = EntityMapper.ToDomain(entity);

                // Add to cache if available
                CacheService?.AddToCache(cacheKey, domainEntity, DefaultCacheDuration);

                return domainEntity;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting {typeof(TDomain).Name} by ID {id}");
                return null;
            }
        }

        /// <summary>
        /// Gets all entities with optional pagination
        /// </summary>
        public virtual async Task<IEnumerable<TDomain>> GetAllAsync(int skip = 0, int take = 50)
        {
            string cacheKey = $"{CollectionPath}_all_{skip}_{take}";

            // Try to get from cache first if available
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<TDomain>>(cacheKey, out var cachedEntities))
            {
                Logger.LogDebug($"Cache hit for all {typeof(TDomain).Name} entities (skip:{skip}, take:{take})");
                return cachedEntities;
            }

            try
            {
                var parameters = new QueryParameters
                {
                    Skip = skip,
                    Take = take
                };

                var entities = await DataStore.GetCollectionAsync<TEntity>(CollectionPath, parameters);

                var domainEntities = entities
                    .Where(e => e != null)
                    .Select(e => EntityMapper.ToDomain(e))
                    .Where(d => d != null)
                    .ToList();

                // Add to cache with short expiration for collection data
                CacheService?.AddToCache(cacheKey, domainEntities, TimeSpan.FromMinutes(5));

                return domainEntities;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting all {typeof(TDomain).Name} entities");
                return Enumerable.Empty<TDomain>();
            }
        }

        /// <summary>
        /// Creates a new entity
        /// </summary>
        public virtual async Task<string> CreateAsync(TDomain domain)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            try
            {
                var entity = EntityMapper.ToEntity(domain);

                // Generate ID if needed
                string id = domain.Id;
                if (string.IsNullOrEmpty(id))
                {
                    id = Guid.NewGuid().ToString();
                    domain.Id = id;

                    // Update entity ID
                    var entityIdProperty = typeof(TEntity).GetProperty("Id");
                    if (entityIdProperty != null)
                    {
                        entityIdProperty.SetValue(entity, id);
                    }
                }

                await DataStore.SetEntityAsync($"{CollectionPath}/{id}", entity);

                // Invalidate collection cache
                InvalidateCollectionCache();

                // Cache the new entity
                string cacheKey = $"{CollectionPath}_{id}";
                CacheService?.AddToCache(cacheKey, domain, DefaultCacheDuration);

                return id;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error creating {typeof(TDomain).Name}");
                throw;
            }
        }

        /// <summary>
        /// Updates an existing entity
        /// </summary>
        public virtual async Task<bool> UpdateAsync(TDomain domain)
        {
            if (domain == null)
            {
                throw new ArgumentNullException(nameof(domain));
            }

            var id = domain.Id;
            if (string.IsNullOrEmpty(id))
            {
                Logger.LogWarning($"Attempted to update {typeof(TDomain).Name} with null or empty ID");
                return false;
            }

            try
            {
                var entity = EntityMapper.ToEntity(domain);

                // Check if entity is versioned
                if (entity is IVersionedEntity versionedEntity)
                {
                    versionedEntity.Version += 1;
                    versionedEntity.LastModified = DateTime.UtcNow;
                }

                await DataStore.SetEntityAsync($"{CollectionPath}/{id}", entity);

                // Invalidate and update caches
                string cacheKey = $"{CollectionPath}_{id}";
                CacheService?.InvalidateCache(cacheKey);
                CacheService?.AddToCache(cacheKey, domain, DefaultCacheDuration);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error updating {typeof(TDomain).Name} with ID {id}");
                return false;
            }
        }

        /// <summary>
        /// Deletes an entity by ID
        /// </summary>
        public virtual async Task<bool> DeleteAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Logger.LogWarning($"Attempted to delete {typeof(TDomain).Name} with null or empty ID");
                return false;
            }

            try
            {
                await DataStore.DeleteEntityAsync($"{CollectionPath}/{id}");

                // Invalidate caches
                string cacheKey = $"{CollectionPath}_{id}";
                CacheService?.InvalidateCache(cacheKey);

                // Invalidate collection cache
                InvalidateCollectionCache();

                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error deleting {typeof(TDomain).Name} with ID {id}");
                return false;
            }
        }

        /// <summary>
        /// Invalidates all collection-related cache entries
        /// </summary>
        protected virtual void InvalidateCollectionCache()
        {
            // Skip if no cache service
            if (CacheService == null) return;

            // Pattern match all collection cache entries
            string pattern = $"{CollectionPath}_all_";
            CacheService.InvalidateCachePattern(pattern);
        }

        /// <summary>
        /// Performs an atomic update using optimistic concurrency
        /// </summary>
        protected virtual async Task<bool> AtomicUpdateAsync(string id, Func<TDomain, TDomain> updateFunc, int maxRetries = 3)
        {
            int attempt = 0;
            while (attempt < maxRetries)
            {
                try
                {
                    attempt++;

                    // Get current entity
                    var current = await GetByIdAsync(id);
                    if (current == null)
                    {
                        return false;
                    }

                    // Obtain current version if entity supports versioning
                    int currentVersion = 0;
                    if (current is IVersionedEntity versionedEntity)
                    {
                        currentVersion = versionedEntity.Version;
                    }

                    // Apply update function
                    var updated = updateFunc(current);

                    // Use a transaction if available
                    using var transaction = await DataStore.BeginTransactionAsync();

                    // Re-fetch within transaction to verify version
                    var entity = await transaction.GetEntityAsync<TEntity>($"{CollectionPath}/{id}");

                    // Check version if applicable
                    if (entity is IVersionedEntity dbVersionedEntity && currentVersion != dbVersionedEntity.Version)
                    {
                        throw new ConcurrencyException($"Version mismatch for {typeof(TDomain).Name} with ID {id}");
                    }

                    // Convert and save
                    var updatedEntity = EntityMapper.ToEntity(updated);

                    // Update version
                    if (updatedEntity is IVersionedEntity versionedUpdatedEntity)
                    {
                        versionedUpdatedEntity.Version = currentVersion + 1;
                        versionedUpdatedEntity.LastModified = DateTime.UtcNow;
                    }

                    await transaction.SetEntityAsync($"{CollectionPath}/{id}", updatedEntity);
                    await transaction.CommitAsync();

                    // Update cache
                    string cacheKey = $"{CollectionPath}_{id}";
                    CacheService?.InvalidateCache(cacheKey);
                    CacheService?.AddToCache(cacheKey, updated, DefaultCacheDuration);

                    // Invalidate collection cache
                    InvalidateCollectionCache();

                    return true;
                }
                catch (ConcurrencyException ex)
                {
                    Logger.LogWarning(ex, $"Concurrency conflict during update of {typeof(TDomain).Name} with ID {id} (attempt {attempt}/{maxRetries})");

                    if (attempt >= maxRetries)
                    {
                        Logger.LogError(ex, $"Max retries reached for atomic update of {typeof(TDomain).Name} with ID {id}");
                        return false;
                    }

                    // Exponential backoff
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 50));
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"Error performing atomic update of {typeof(TDomain).Name} with ID {id}");
                    return false;
                }
            }

            return false;
        }
    }
}