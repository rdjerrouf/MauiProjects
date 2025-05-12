using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MarketDZ.Services.Core.Interfaces.Cache;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Implementations
{
    /// <summary>
    /// In-memory implementation of the cache service
    /// </summary>
    public class InMemoryCacheService : ICacheService
    {
        private readonly ConcurrentDictionary<string, CacheItem> _cache = new ConcurrentDictionary<string, CacheItem>();
        private readonly ILogger<InMemoryCacheService> _logger;
        private readonly int _maxCacheSize;
        private readonly TimeSpan _defaultExpiration;
        private long _currentSize = 0;

        // Cache policy defaults
        private readonly Dictionary<CachePolicy, TimeSpan> _policyExpirations = new Dictionary<CachePolicy, TimeSpan>
        {
            { CachePolicy.Volatile, TimeSpan.FromMinutes(1) },
            { CachePolicy.Normal, TimeSpan.FromMinutes(10) },
            { CachePolicy.Persistent, TimeSpan.FromHours(1) },
            { CachePolicy.Permanent, TimeSpan.FromDays(7) }
        };

        public InMemoryCacheService(
            ILogger<InMemoryCacheService> logger,
            int maxCacheSizeMB = 100,
            int defaultExpirationMinutes = 10)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maxCacheSize = maxCacheSizeMB * 1024 * 1024; // Convert MB to bytes
            _defaultExpiration = TimeSpan.FromMinutes(defaultExpirationMinutes);

            _logger.LogInformation("Initialized InMemoryCacheService with max size {MaxSizeMB}MB and default expiration {ExpirationMinutes}min",
                maxCacheSizeMB, defaultExpirationMinutes);
        }

        /// <summary>
        /// Tries to get a value from the cache
        /// </summary>
        public bool TryGetFromCache<T>(string key, out T value)
        {
            value = default;

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (!_cache.TryGetValue(key, out var item) || item.IsExpired)
            {
                // Remove if expired
                if (item != null && item.IsExpired)
                {
                    RemoveFromCache(key);
                }

                return false;
            }

            try
            {
                value = (T)item.Value;

                // Update last access time for LRU
                item.LastAccessed = DateTime.UtcNow;

                return true;
            }
            catch (InvalidCastException)
            {
                _logger.LogWarning("Failed to cast cached item with key {Key} to type {Type}", key, typeof(T).Name);
                return false;
            }
        }

        /// <summary>
        /// Adds a value to the cache with optional expiration
        /// </summary>
        public void AddToCache<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return;
            }

            var expirationTime = expiration ?? _defaultExpiration;

            var item = new CacheItem
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expirationTime),
                Created = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                EstimatedSize = EstimateObjectSize(value)
            };

            // Trim cache if needed before adding
            EnsureCacheSize(item.EstimatedSize);

            _cache[key] = item;
            Interlocked.Add(ref _currentSize, item.EstimatedSize);
        }

        /// <summary>
        /// Adds a value to the cache with a policy and related keys
        /// </summary>
        public void AddToCache<T>(string key, T value, CachePolicy policy, IEnumerable<string> relatedKeys = null)
        {
            if (string.IsNullOrEmpty(key) || value == null)
            {
                return;
            }

            // Get expiration time from policy
            var expirationTime = _policyExpirations.TryGetValue(policy, out var expiration)
                ? expiration
                : _defaultExpiration;

            var item = new CacheItem
            {
                Value = value,
                ExpiresAt = DateTime.UtcNow.Add(expirationTime),
                Created = DateTime.UtcNow,
                LastAccessed = DateTime.UtcNow,
                EstimatedSize = EstimateObjectSize(value),
                RelatedKeys = relatedKeys?.ToList() ?? new List<string>()
            };

            // Trim cache if needed before adding
            EnsureCacheSize(item.EstimatedSize);

            _cache[key] = item;
            Interlocked.Add(ref _currentSize, item.EstimatedSize);
        }

        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        public void InvalidateCache(string key)
        {
            if (string.IsNullOrEmpty(key) || !_cache.ContainsKey(key))
            {
                return;
            }

            RemoveFromCache(key);
        }

        /// <summary>
        /// Invalidates all cache entries matching a pattern
        /// </summary>
        public void InvalidateCachePattern(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return;
            }

            try
            {
                var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                // Find all matching keys
                var keysToRemove = _cache.Keys.Where(k => regex.IsMatch(k)).ToList();

                // Remove each key
                foreach (var key in keysToRemove)
                {
                    RemoveFromCache(key);
                }

                _logger.LogDebug("Invalidated {Count} cache entries matching pattern {Pattern}", keysToRemove.Count, pattern);
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
            InvalidateCachePattern("items_filtered_");
            InvalidateCachePattern("items_category_");
            InvalidateCachePattern("items_state_");
            InvalidateCachePattern("items_search_");
            InvalidateCachePattern("items_paginated_");
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

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Every minute, clean expired items
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);

                    int removed = RemoveExpiredItems();

                    if (removed > 0)
                    {
                        _logger.LogDebug("Removed {Count} expired cache items", removed);
                    }

                    // Every 10 minutes, trim cache if needed
                    if (DateTime.UtcNow.Minute % 10 == 0)
                    {
                        TrimCache();
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
            if (_currentSize <= _maxCacheSize * 0.9)
            {
                // Cache is within acceptable limits
                return;
            }

            _logger.LogInformation("Trimming cache from {CurrentSizeMB}MB to {TargetSizeMB}MB",
                _currentSize / (1024 * 1024), (_maxCacheSize * 0.8) / (1024 * 1024));

            // Target is 80% of max to avoid frequent trims
            long targetSize = (long)(_maxCacheSize * 0.8);

            // Get all items sorted by last access time (oldest first)
            var itemsToTrim = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Select(kvp => kvp.Key)
                .ToList();

            // Remove items until we're under target size
            foreach (var key in itemsToTrim)
            {
                if (_currentSize <= targetSize)
                {
                    break;
                }

                RemoveFromCache(key);
            }

            _logger.LogInformation("Cache trimmed to {CurrentSizeMB}MB", _currentSize / (1024 * 1024));
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

            // Basic size estimation by type
            if (obj is string str)
            {
                return 24 + (str.Length * 2); // String overhead + 2 bytes per char
            }

            if (obj is ValueType)
            {
                return 16; // Approximate size for simple value types
            }

            if (obj is ICollection<object> collection)
            {
                // Collection overhead + recursive size estimation
                int size = 48;
                foreach (var item in collection)
                {
                    size += EstimateObjectSize(item);
                }
                return size;
            }

            // Default estimation for complex objects
            // This is a very rough estimate and should be improved for your specific data types
            return 256;
        }

        /// <summary>
        /// Determines appropriate cache expiration time based on the content
        /// </summary>
        public TimeSpan DetermineExpirationTime<T>(string key, T value)
        {
            // Implement custom logic based on key/value patterns
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
        /// Removes a cache item and updates the size counter
        /// </summary>
        private void RemoveFromCache(string key)
        {
            if (_cache.TryRemove(key, out var item))
            {
                Interlocked.Add(ref _currentSize, -item.EstimatedSize);

                // Also invalidate related keys if any
                if (item.RelatedKeys != null && item.RelatedKeys.Any())
                {
                    foreach (var relatedKey in item.RelatedKeys)
                    {
                        RemoveFromCache(relatedKey);
                    }
                }
            }
        }

        /// <summary>
        /// Removes all expired items from the cache
        /// </summary>
        private int RemoveExpiredItems()
        {
            var now = DateTime.UtcNow;

            // Find all expired keys
            var expiredKeys = _cache
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            // Remove each expired key
            foreach (var key in expiredKeys)
            {
                RemoveFromCache(key);
            }

            return expiredKeys.Count;
        }

        /// <summary>
        /// Ensures the cache has enough space for a new item
        /// </summary>
        private void EnsureCacheSize(int newItemSize)
        {
            if (_currentSize + newItemSize <= _maxCacheSize)
            {
                return;
            }

            // Need to make room
            var targetSize = Math.Max(_maxCacheSize * 0.8, _maxCacheSize - newItemSize);

            // Get LRU items
            var itemsToRemove = _cache
                .OrderBy(kvp => kvp.Value.LastAccessed)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in itemsToRemove)
            {
                RemoveFromCache(key);

                if (_currentSize <= targetSize)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            _cache.Clear();
            Interlocked.Exchange(ref _currentSize, 0);
        }
    }

    /// <summary>
    /// Represents a cached item with metadata
    /// </summary>
    internal class CacheItem
    {
        /// <summary>
        /// The cached value
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// When the item expires
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// When the item was created
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// When the item was last accessed
        /// </summary>
        public DateTime LastAccessed { get; set; }

        /// <summary>
        /// Estimated size of the item in bytes
        /// </summary>
        public int EstimatedSize { get; set; }

        /// <summary>
        /// Related cache keys that should be invalidated with this item
        /// </summary>
        public List<string> RelatedKeys { get; set; } = new List<string>();

        /// <summary>
        /// Checks if the item is expired
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}
