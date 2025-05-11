using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MarketDZ.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    /// <summary>
    /// Memory-based implementation of ICacheService
    /// </summary>
    public class MemoryCacheService : ICacheService
    {
        private readonly ILogger<MemoryCacheService> _logger;

        // Cache configuration
        private readonly TimeSpan _shortCacheExpiration = TimeSpan.FromMinutes(1);   // For volatile data
        private readonly TimeSpan _mediumCacheExpiration = TimeSpan.FromMinutes(5);  // Default
        private readonly TimeSpan _longCacheExpiration = TimeSpan.FromHours(1);      // For stable data

        // Threshold for compression (in bytes)
        private readonly int _compressionThreshold = 10 * 1024; // 10KB

        // Maximum cache size (in bytes)
        private readonly long _maxCacheSize = 100 * 1024 * 1024; // 100MB
        private long _estimatedCacheSize = 0;

        // Enhanced cache with tracking
        private readonly ConcurrentDictionary<string, object> _cache = new ConcurrentDictionary<string, object>();
        private readonly ConcurrentDictionary<string, int> _cacheHitCounters = new ConcurrentDictionary<string, int>();

        private CancellationTokenSource _maintenanceCts;

        /// <summary>
        /// Creates a new instance of MemoryCacheService
        /// </summary>
        /// <param name="logger">Logger</param>
        public MemoryCacheService(ILogger<MemoryCacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maintenanceCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Tries to get a value from the cache
        /// </summary>
        public bool TryGetFromCache<T>(string key, out T value)
        {
            value = default;

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (cacheItem is EnhancedCacheItem<T> typedItem && !typedItem.IsExpired)
                {
                    // Increment hit counter for this cache key
                    _cacheHitCounters.AddOrUpdate(key, 1, (_, count) => count + 1);

                    // Get the value (updates LastAccessed internally)
                    value = typedItem.GetValue();
                    return true;
                }

                // Remove expired or wrongly typed item
                _cache.TryRemove(key, out _);
            }

            return false;
        }

        /// <summary>
        /// Adds a value to the cache with optional expiration
        /// </summary>
        public void AddToCache<T>(string key, T value, TimeSpan? expiration = null)
        {
            // Skip null values
            if (value == null)
                return;

            // Determine appropriate expiration
            var actualExpiration = expiration ?? DetermineExpirationTime(key, value);

            // Estimate size to determine if compression should be used
            var estimatedSize = EstimateObjectSize(value);
            bool shouldCompress = estimatedSize > _compressionThreshold;

            // Create cache item (with compression if needed)
            var cacheItem = new EnhancedCacheItem<T>(value, actualExpiration, shouldCompress);

            // Update cache
            _cache[key] = cacheItem;

            // Update estimated cache size
            _estimatedCacheSize += estimatedSize;

            // Check if cache needs trimming
            if (_estimatedCacheSize > _maxCacheSize)
            {
                TrimCache();
            }

            // Log caching details
            if (shouldCompress)
            {
                _logger.LogDebug("Added compressed item to cache: {Key}, Size: ~{Size}KB, Expires: {Expiration}",
                    key, estimatedSize / 1024, DateTime.UtcNow.Add(actualExpiration));
            }
        }

        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        public void InvalidateCache(string key)
        {
            if (_cache.TryRemove(key, out var removedItem))
            {
                var size = EstimateObjectSize(removedItem);
                _estimatedCacheSize = Math.Max(0, _estimatedCacheSize - size);
                _cacheHitCounters.TryRemove(key, out _);

                _logger.LogDebug("Cache invalidated: {Key}, Freed ~{SizeKB}KB", key, size / 1024);
            }
        }

        /// <summary>
        /// Invalidates all cache entries matching a pattern
        /// </summary>
        public void InvalidateCachePattern(string pattern)
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.Contains(pattern))
                .ToList();

            long totalFreed = 0;
            foreach (var key in keysToRemove)
            {
                if (_cache.TryRemove(key, out var removedItem))
                {
                    var size = EstimateObjectSize(removedItem);
                    totalFreed += size;
                    _estimatedCacheSize = Math.Max(0, _estimatedCacheSize - size);
                    _cacheHitCounters.TryRemove(key, out _);
                }
            }

            if (keysToRemove.Count > 0)
            {
                _logger.LogInformation("Invalidated {Count} cache entries matching pattern {Pattern}, freed ~{FreedMB}MB",
                    keysToRemove.Count, pattern, totalFreed / (1024 * 1024));
            }
        }

        /// <summary>
        /// Invalidates all filter-related cache entries
        /// </summary>
        public void InvalidateAllFilterCaches()
        {
            InvalidateCachePattern("filtered_items_");
        }

        /// <summary>
        /// Warms up the cache with common queries
        /// </summary>
        public Task WarmUpCacheAsync()
        {
            // This needs to be implemented according to your application's needs
            _logger.LogInformation("Cache warm-up requested. Override this method to implement cache warm-up.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Starts a background cache maintenance task
        /// </summary>
        public async Task StartMaintenanceAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Wait 15 minutes between maintenance cycles
                    await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);

                    try
                    {
                        _logger.LogDebug("Running cache maintenance");

                        // Check for expired items
                        var expiredCount = 0;
                        var keysToCheck = _cache.Keys.ToList();

                        foreach (var key in keysToCheck)
                        {
                            if (_cache.TryGetValue(key, out var cacheItem))
                            {
                                bool isExpired = false;

                                // Check if the item is expired based on its type
                                if (cacheItem is IExpirableCacheItem expirable)
                                {
                                    isExpired = expirable.IsExpired;
                                }

                                if (isExpired)
                                {
                                    _cache.TryRemove(key, out _);
                                    _cacheHitCounters.TryRemove(key, out _);
                                    expiredCount++;
                                }
                            }
                        }

                        // Log maintenance results
                        if (expiredCount > 0)
                        {
                            _logger.LogInformation("Cache maintenance: Removed {Count} expired items", expiredCount);
                        }

                        // Re-warm cache if it's too empty
                        if (_cache.Count < 10)
                        {
                            await WarmUpCacheAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during cache maintenance");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
        }

        /// <summary>
        /// Trims the cache to stay within size limits
        /// </summary>
        public void TrimCache()
        {
            _logger.LogInformation("Cache size exceeded limit. Current: {CurrentSize}MB, Max: {MaxSize}MB. Trimming...",
                _estimatedCacheSize / (1024 * 1024), _maxCacheSize / (1024 * 1024));

            try
            {
                // Get cache entries sorted by access frequency and time
                var entries = _cache.ToList();

                // Calculate how many entries to remove (25% of cache)
                int removeCount = Math.Max(entries.Count / 4, 1);

                // Sort by access count (least used first) and then by last accessed
                var itemsToRemove = entries
                    .Where(kv => kv.Value is ITrackableCacheItem)
                    .Select(kv => new
                    {
                        Key = kv.Key,
                        Item = kv.Value as ITrackableCacheItem
                    })
                    .OrderBy(x => x.Item.AccessCount)
                    .ThenBy(x => x.Item.LastAccessed)
                    .Take(removeCount)
                    .Select(x => x.Key)
                    .ToList();

                // Remove selected items
                long bytesFreed = 0;
                foreach (var key in itemsToRemove)
                {
                    if (_cache.TryRemove(key, out var removedItem))
                    {
                        bytesFreed += EstimateObjectSize(removedItem);
                        _cacheHitCounters.TryRemove(key, out _);
                    }
                }

                // Update estimated size
                _estimatedCacheSize = Math.Max(0, _estimatedCacheSize - bytesFreed);

                _logger.LogInformation("Cache trim complete: Removed {RemovedCount} items, Freed ~{FreedMB}MB",
                    itemsToRemove.Count, bytesFreed / (1024 * 1024));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache trimming");

                // Fallback: clear entire cache if trimming fails
                _cache.Clear();
                _cacheHitCounters.Clear();
                _estimatedCacheSize = 0;

                _logger.LogWarning("Cache cleared completely due to trimming error");
            }
        }

        /// <summary>
        /// Estimates the size of an object in bytes
        /// </summary>
        public int EstimateObjectSize(object obj)
        {
            if (obj == null)
                return 0;

            try
            {
                // Rough estimation using JSON serialization
                return JsonConvert.SerializeObject(obj).Length;
            }
            catch
            {
                // Fallback for non-serializable objects
                return 1000; // Default assumption
            }
        }

        /// <summary>
        /// Determines appropriate cache expiration time based on the content
        /// </summary>
        public TimeSpan DetermineExpirationTime<T>(string key, T value)
        {
            // Use different expiration times based on the key pattern
            if (key.Contains("filtered_items_"))
            {
                if (key.Contains("SortOption.DateNewest"))
                    return _shortCacheExpiration; // Recent items expire quickly

                if (key.Contains("\"Category\":") && !key.Contains("\"SearchText\":"))
                    return _longCacheExpiration; // Category browsing is more stable
            }

            // User data might change frequently
            if (key.StartsWith("get:users/"))
                return _shortCacheExpiration;

            // Collection results might contain any changes
            if (key.StartsWith("collection:"))
                return _shortCacheExpiration;

            // Most data can use the medium expiration
            return _mediumCacheExpiration;
        }

        /// <summary>
        /// Disposes the cache service and cleans up resources
        /// </summary>
        public void Dispose()
        {
            _maintenanceCts?.Cancel();
            _maintenanceCts?.Dispose();
            _maintenanceCts = null;

            _cache.Clear();
            _cacheHitCounters.Clear();
            _estimatedCacheSize = 0;
        }

        void ICacheService.AddToCache(string cacheKey, object result, CachePolicy @volatile, IEnumerable<string> enumerable)
        {
            throw new NotImplementedException();
        }

        public void AddToCache(string cacheKey, IEnumerable<Item> items, CachePolicy @volatile)
        {
            throw new NotImplementedException();
        }

        #region Enhanced Caching Classes

        /// <summary>
        /// Interface for expirable cache items
        /// </summary>
        private interface IExpirableCacheItem
        {
            bool IsExpired { get; }
        }

        /// <summary>
        /// Interface for trackable cache items
        /// </summary>
        private interface ITrackableCacheItem
        {
            DateTime LastAccessed { get; }
            int AccessCount { get; }
        }

        /// <summary>
        /// Enhanced cache item with optional compression
        /// </summary>
        private class EnhancedCacheItem<T> : IExpirableCacheItem, ITrackableCacheItem
        {
            // The actual value (only stored if not compressed)
            private T _value;

            // Compressed data (only stored if compression is used)
            private byte[] _compressedData;

            public DateTime ExpiresAt { get; }
            public bool IsCompressed { get; }
            public DateTime LastAccessed { get; private set; }
            public int AccessCount { get; private set; }

            // Create a non-compressed cache item
            public EnhancedCacheItem(T value, TimeSpan expiration)
            {
                _value = value;
                _compressedData = null;
                IsCompressed = false;
                ExpiresAt = DateTime.UtcNow.Add(expiration);
                LastAccessed = DateTime.UtcNow;
                AccessCount = 0;
            }

            // Create a compressed cache item
            public EnhancedCacheItem(T value, TimeSpan expiration, bool compress)
            {
                if (compress)
                {
                    _value = default;
                    _compressedData = CompressObject(value);
                    IsCompressed = true;
                }
                else
                {
                    _value = value;
                    _compressedData = null;
                    IsCompressed = false;
                }

                ExpiresAt = DateTime.UtcNow.Add(expiration);
                LastAccessed = DateTime.UtcNow;
                AccessCount = 0;
            }

            public bool IsExpired => DateTime.UtcNow > ExpiresAt;

            public T GetValue()
            {
                LastAccessed = DateTime.UtcNow;
                AccessCount++;

                if (IsCompressed)
                {
                    return DecompressObject<T>(_compressedData);
                }

                return _value;
            }

            // Compress an object to a byte array
            private byte[] CompressObject<TObj>(TObj obj)
            {
                var jsonData = JsonConvert.SerializeObject(obj);
                var rawData = Encoding.UTF8.GetBytes(jsonData);

                using var memory = new MemoryStream();
                using (var gzip = new GZipStream(memory, CompressionLevel.Optimal))
                {
                    gzip.Write(rawData, 0, rawData.Length);
                }

                return memory.ToArray();
            }

            // Decompress a byte array to an object
            private TObj DecompressObject<TObj>(byte[] compressedData)
            {
                using var memory = new MemoryStream(compressedData);
                using var outputMemory = new MemoryStream();
                using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
                {
                    gzip.CopyTo(outputMemory);
                }

                var rawData = outputMemory.ToArray();
                var jsonData = Encoding.UTF8.GetString(rawData);
                return JsonConvert.DeserializeObject<TObj>(jsonData);
            }
        }

        #endregion
    }
}