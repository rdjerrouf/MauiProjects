using System;
using System.Threading;
using System.Threading.Tasks;
using MarketDZ.Models;

namespace MarketDZ.Services.Core.Interfaces.Cache  
{
    /// <summary>
    /// Defines a cache service interface
    /// </summary>
    public interface ICacheService : IDisposable
    {
        /// <summary>
        /// Tries to get a value from the cache
        /// </summary>
        /// <typeparam name="T">Type of the cached value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Output value if found</param>
        /// <returns>True if the value was found, false otherwise</returns>
        bool TryGetFromCache<T>(string key, out T value);

        /// <summary>
        /// Adds a value to the cache with optional expiration
        /// </summary>
        /// <typeparam name="T">Type of the value to cache</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to cache</param>
        /// <param name="expiration">Optional expiration timespan</param>
        void AddToCache<T>(string key, T value, TimeSpan? expiration = null);

        /// <summary>
        /// Invalidates a specific cache entry
        /// </summary>
        /// <param name="key">Cache key to invalidate</param>
        void InvalidateCache(string key);

        /// <summary>
        /// Invalidates all cache entries matching a pattern
        /// </summary>
        /// <param name="pattern">Pattern to match against cache keys</param>
        void InvalidateCachePattern(string pattern);

        /// <summary>
        /// Invalidates all filter-related cache entries
        /// </summary>
        void InvalidateAllFilterCaches();

        /// <summary>
        /// Warms up the cache with common queries
        /// </summary>
        Task WarmUpCacheAsync();

        /// <summary>
        /// Starts a background cache maintenance task
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        Task StartMaintenanceAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Trims the cache to stay within size limits
        /// </summary>
        void TrimCache();

        /// <summary>
        /// Estimates the size of an object in bytes
        /// </summary>
        /// <param name="obj">Object to measure</param>
        /// <returns>Estimated size in bytes</returns>
        int EstimateObjectSize(object obj);

        /// <summary>
        /// Determines appropriate cache expiration time based on the content
        /// </summary>
        /// <typeparam name="T">Type of the value</typeparam>
        /// <param name="key">Cache key</param>
        /// <param name="value">Value to be cached</param>
        /// <returns>Appropriate expiration timespan</returns>
        TimeSpan DetermineExpirationTime<T>(string key, T value);
        void AddToCache(string cacheKey, object result, CachePolicy @volatile, IEnumerable<string> enumerable);
        void AddToCache(string cacheKey, IEnumerable<Item> items, CachePolicy @volatile);
    }
}