using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Services.Core.Interfaces.Cache;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Implementations
{
    /// <summary>
    /// Redis implementation of the cache service
    /// </summary>
    public class RedisCacheService : ICacheService
    {
        private readonly ILogger<RedisCacheService> _logger;
        private readonly string _connectionString;
        private readonly TimeSpan _defaultExpiration;
        private StackExchange.Redis.ConnectionMultiplexer _redis;
        private StackExchange.Redis.IDatabase _db;

        // Cache policy defaults
        private readonly Dictionary<CachePolicy, TimeSpan> _policyExpirations = new Dictionary<CachePolicy, TimeSpan>
        {
            { CachePolicy.Volatile, TimeSpan.FromMinutes(1) },
            { CachePolicy.Normal, TimeSpan.FromMinutes(10) },
            { CachePolicy.Persistent, TimeSpan.FromHours(1) },
            { CachePolicy.Permanent, TimeSpan.FromDays(7) }
        };

        public RedisCacheService(
            ILogger<RedisCacheService> logger,
            string connectionString,
            int defaultExpirationMinutes = 10)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _defaultExpiration = TimeSpan.FromMinutes(defaultExpirationMinutes);

            _logger.LogInformation("Initializing RedisCacheService with default expiration {ExpirationMinutes}min",
                defaultExpirationMinutes);

            Initialize();
        }

        /// <summary>
        /// Initializes the Redis connection
        /// </summary>
        private void Initialize()
        {
            try
            {
                _redis = StackExchange.Redis.ConnectionMultiplexer.Connect(_connectionString);
                _db = _redis.GetDatabase();

                _logger.LogInformation("Redis connection established successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Redis connection: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Tries to get a value from the cache
        /// </summary>
        public bool TryGetFromCache<T>(string key, out T value)
        {
            value = default;

            if (string.IsNullOrEmpty(key) || _db == null)
            {
                return false;
            }

            try
            {
                var redisValue = _db.StringGet(key);

                if (redisValue.IsNull)
                {
                    return false;
                }

                // Deserialize the value
                var serialized = redisValue.ToString();
                value = System.Text.Json.JsonSerializer.Deserialize<T>(serialized);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache value for key {Key}: {Message}", key, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Adds a value to the cache with optional expiration
        /// </summary>
        public void AddToCache<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key) || value == null || _db == null)
            {
                return;
            }

            var expirationTime = expiration ?? _defaultExpiration;

            try
            {
                // Serialize the value
                var serialized = System.Text.Json.JsonSerializer.Serialize(value);

                // Store in Redis with expiration
                _db.StringSet(key, serialized, expirationTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add value to cache for key {Key}: {Message}", key, ex.Message);
            }
        }

        /// <summary>
        /// Adds a value to the cache with a policy and related keys
        /// </summary>
        public void AddToCache<T>(string key, T value, CachePolicy policy, IEnumerable<string> relatedKeys = null)
        {
            if (string.IsNullOrEmpty(key) || value == null || _db == null)
            {
                return;
            }

            // Get expiration time from policy
            var expirationTime = _policyExpirations.TryGetValue(policy, out var expiration)
                ? expiration
                : _defaultExpiration;

            try
            {
                // Serialize the value
                var serialized = System.Text.Json.JsonSerializer.Serialize(value);

                // Store in Redis with expiration
                _db.StringSet(key, serialized, expirationTime);

                // Store related keys if any
                if (relatedKeys != null && relatedKeys.Any())
                {
                    var relatedKeySet = $"{key}:related";
                    _db.KeyDelete(relatedKeySet); // Remove any existing related keys

                    foreach (var relatedKey in relatedKeys)
                    {
                        _db.SetAdd(relatedKeySet, relatedKey);
                    }

                    // Set expiration on the related key set
                    _db.KeyExpire(relatedKeySet, expirationTime);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add value to cache for key {Key} with policy {Policy}: {Message}",
                    key, policy, ex.Message);
            }
        }

        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        public void InvalidateCache(string key)
        {
            if (string.IsNullOrEmpty(key) || _db == null)
            {
                return;
            }

            try
            {
                // Check for related keys
                var relatedKeySet = $"{key}:related";
                var relatedKeys = _db.SetMembers(relatedKeySet);

                // Delete the main key
                _db.KeyDelete(key);

                // Delete related keys and the set itself
                if (relatedKeys.Length > 0)
                {
                    foreach (var relatedKey in relatedKeys)
                    {
                        _db.KeyDelete(relatedKey.ToString());
                    }

                    _db.KeyDelete(relatedKeySet);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate cache for key {Key}: {Message}", key, ex.Message);
            }
        }

        /// <summary>
        /// Invalidates all cache entries matching a pattern
        /// </summary>
        public void InvalidateCachePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern) || _db == null)
            {
                return;
            }

            try
            {
                // Convert .NET regex pattern to Redis pattern
                // Note: Redis uses a different pattern syntax, so this is a simplified conversion
                var redisPattern = pattern
                    .Replace(".*", "*")
                    .Replace(".+", "*")
                    .Replace("\\d+", "*");

                // Use Redis SCAN to find matching keys
                var server = _redis.GetServer(_redis.GetEndPoints().First());
                var matchingKeys = server.Keys(pattern: redisPattern).ToArray();

                // Delete all matching keys
                if (matchingKeys.Length > 0)
                {
                    _db.KeyDelete(matchingKeys);
                    _logger.LogDebug("Invalidated {Count} cache entries matching pattern {Pattern}", matchingKeys.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating cache with pattern {Pattern}: {Message}", pattern, ex.Message);
            }
        }

        /// <summary>
        /// Invalidates all filter-related cache entries
        /// </summary>
        public void InvalidateAllFilterCaches()
        {
            // Invalidate any cache keys related to filtering
            InvalidateCachePattern("items_filtered_*");
            InvalidateCachePattern("items_category_*");
            InvalidateCachePattern("items_state_*");
            InvalidateCachePattern("items_search_*");
            InvalidateCachePattern("items_paginated_*");
        }

        /// <summary>
        /// Warms up the cache with common queries
        /// </summary>
        public async Task WarmUpCacheAsync()
        {
            _logger.LogInformation("Warming up cache not implemented in this version");
            await Task.CompletedTask;
        }

        /// <summary>
        /// Starts a background cache maintenance task
        /// </summary>
        public async Task StartMaintenanceAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting cache maintenance task");

            // Redis handles expiration automatically, so we just need minimal maintenance
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check connection health periodically
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    if (!_redis.IsConnected)
                    {
                        _logger.LogWarning("Redis connection lost, attempting to reconnect");
                        Initialize();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Normal cancellation
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in cache maintenance: {Message}", ex.Message);
                }
            }

            _logger.LogInformation("Cache maintenance task stopped");
        }

        /// <summary>
        /// Trims the cache to stay within size limits
        /// </summary>
        public void TrimCache()
        {
            // Redis handles memory management automatically with its maxmemory policy
            // This method is not needed for Redis
        }

        /// <summary>
        /// Estimates the size of an object in bytes
        /// </summary>
        public int EstimateObjectSize(object obj)
        {
            if (obj == null)
            {
                return 0;
            }

            // For Redis, we can estimate based on the serialized size
            try
            {
                var serialized = System.Text.Json.JsonSerializer.Serialize(obj);
                return serialized.Length * 2; // UTF-16 encoding (2 bytes per char)
            }
            catch
            {
                // Fallback to a rough estimate
                return 256;
            }
        }

        /// <summary>
        /// Determines appropriate cache expiration time based on the content
        /// </summary>
        public TimeSpan DetermineExpirationTime<T>(string key, T value)
        {
            // Similar to in-memory cache logic
            if (key.StartsWith("user_") && !key.Contains("_temp"))
            {
                return TimeSpan.FromMinutes(30); // User data expires slower
            }

            if (key.StartsWith("items_filtered_") || key.StartsWith("items_search_"))
            {
                return TimeSpan.FromMinutes(2); // Search results expire quickly
            }

            if (key.StartsWith("items_") && key.Contains("_state_"))
            {
                return TimeSpan.FromMinutes(15); // State-filtered items
            }

            if (key.Contains("_count") || key.Contains("statistics"))
            {
                return TimeSpan.FromMinutes(5); // Stats expire moderately fast
            }

            // Default expiration
            return _defaultExpiration;
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            _redis?.Dispose();
        }
    }
}
