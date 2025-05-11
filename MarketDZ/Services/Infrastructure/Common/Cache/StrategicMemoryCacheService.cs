using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    /// <summary>
    /// Enhanced memory-based implementation of ICacheService with strategic caching
    /// </summary>
    public class StrategicMemoryCacheService : ICacheService, IDisposable
    {
        private readonly ILogger<StrategicMemoryCacheService> _logger;
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly ConcurrentDictionary<string, CacheMetrics> _cacheMetrics = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _cacheDependencies = new();
        private CancellationTokenSource _maintenanceCts;

        // Configuration
        private readonly Dictionary<CachePolicy, TimeSpan> _policyExpirations = new()
        {
            { CachePolicy.Volatile, TimeSpan.FromSeconds(30) },
            { CachePolicy.Moderate, TimeSpan.FromMinutes(5) },
            { CachePolicy.Stable, TimeSpan.FromHours(1) },
            { CachePolicy.Immutable, TimeSpan.FromDays(30) }
        };

        private readonly long _maxCacheSize = 100 * 1024 * 1024; // 100MB
        private long _currentCacheSize = 0;

        public StrategicMemoryCacheService(ILogger<StrategicMemoryCacheService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _maintenanceCts = new CancellationTokenSource();
        }

        /// <summary>
        /// Adds a value to the cache with a specific policy and optional dependencies
        /// </summary>
        public void AddToCache<T>(string key, T value, CachePolicy policy = CachePolicy.Moderate, IEnumerable<string> dependencies = null)
        {
            if (value == null) return;

            TimeSpan expiration = _policyExpirations[policy];
            var cacheItem = new EnhancedCacheItemWithMetrics<T>(value, expiration, false, policy);

            // Add dependencies
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    cacheItem.Dependencies.Add(dependency);
                    _cacheDependencies.AddOrUpdate(dependency,
                        new HashSet<string> { key },
                        (_, set) => { set.Add(key); return set; });
                }
            }

            _cache[key] = cacheItem;
            _currentCacheSize += cacheItem.Size;

            // Initialize metrics
            _cacheMetrics[key] = new CacheMetrics
            {
                Key = key,
                Policy = policy,
                CreateTime = DateTime.UtcNow,
                LastAccessTime = DateTime.UtcNow,
                Hits = 0,
                Misses = 0,
                Size = cacheItem.Size
            };

            if (_currentCacheSize > _maxCacheSize)
            {
                TrimCache();
            }

            _logger.LogDebug("Added to cache: {Key}, Policy: {Policy}, Size: {Size}KB, Expires: {Expiration}",
                key, policy, cacheItem.Size / 1024, expiration);
        }

        /// <summary>
        /// Overload that determines the policy based on expiration time
        /// </summary>
        public void AddToCache<T>(string key, T value, TimeSpan? expiration = null)
        {
            CachePolicy policy = expiration switch
            {
                { TotalSeconds: <= 60 } => CachePolicy.Volatile,
                { TotalMinutes: <= 10 } => CachePolicy.Moderate,
                { TotalHours: <= 2 } => CachePolicy.Stable,
                _ => CachePolicy.Moderate
            };

            AddToCache(key, value, policy);
        }

        public bool TryGetFromCache<T>(string key, out T value)
        {
            value = default;

            if (_cache.TryGetValue(key, out var cacheItem))
            {
                if (cacheItem is EnhancedCacheItemWithMetrics<T> typedItem && !typedItem.IsExpired)
                {
                    value = typedItem.GetValue();
                    typedItem.TotalAccessCount++;

                    // Update metrics
                    if (_cacheMetrics.TryGetValue(key, out var metrics))
                    {
                        metrics.Hits++;
                        metrics.LastAccessTime = DateTime.UtcNow;
                    }

                    return true;
                }

                // Clean up expired item
                InvalidateCache(key);
            }

            // Record miss
            if (_cacheMetrics.TryGetValue(key, out var missMetrics))
            {
                missMetrics.Misses++;
            }

            return false;
        }

        public void InvalidateCache(string key)
        {
            if (_cache.TryRemove(key, out var removedItem))
            {
                if (removedItem is EnhancedCacheItemWithMetrics<object> item)
                {
                    _currentCacheSize -= item.Size;

                    // Handle dependencies
                    foreach (var dependency in item.Dependencies)
                    {
                        if (_cacheDependencies.TryGetValue(dependency, out var dependents))
                        {
                            dependents.Remove(key);
                            if (!dependents.Any())
                            {
                                _cacheDependencies.TryRemove(dependency, out _);
                            }
                        }
                    }
                }

                // Invalidate dependent items
                if (_cacheDependencies.TryGetValue(key, out var dependentKeys))
                {
                    foreach (var dependentKey in dependentKeys)
                    {
                        InvalidateCache(dependentKey);
                    }
                    _cacheDependencies.TryRemove(key, out _);
                }

                _cacheMetrics.TryRemove(key, out _);
                _logger.LogDebug("Invalidated cache: {Key}", key);
            }
        }

        public void InvalidateCachePattern(string pattern)
        {
            var keysToRemove = _cache.Keys
                .Where(k => k.Contains(pattern))
                .ToList();

            foreach (var key in keysToRemove)
            {
                InvalidateCache(key);
            }

            _logger.LogInformation("Invalidated {Count} cache entries matching pattern {Pattern}",
                keysToRemove.Count, pattern);
        }

        public void InvalidateAllFilterCaches()
        {
            InvalidateCachePattern("filtered_items_");
            InvalidateCachePattern("filter:");
        }

        public Task WarmUpCacheAsync() => Task.CompletedTask;

        public int EstimateObjectSize(object obj) => JsonConvert.SerializeObject(obj).Length;

        public TimeSpan DetermineExpirationTime<T>(string key, T value)
        {
            if (key.Contains("filtered_items_") && key.Contains("SortOption.DateNewest"))
                return _policyExpirations[CachePolicy.Volatile];
            if (key.Contains("Category") && !key.Contains("SearchText"))
                return _policyExpirations[CachePolicy.Stable];
            return _policyExpirations[CachePolicy.Moderate];
        }

        public void TrimCache()
        {
            _logger.LogInformation("Trimming cache - Current size: {CurrentSize}MB, Max: {MaxSize}MB",
                _currentCacheSize / (1024 * 1024), _maxCacheSize / (1024 * 1024));

            var items = _cache.ToList();
            var sortedItems = items
                .Select(kv => new
                {
                    Key = kv.Key,
                    Item = kv.Value as EnhancedCacheItemWithMetrics<object>,
                    Metrics = _cacheMetrics.GetValueOrDefault(kv.Key)
                })
                .Where(x => x.Item != null)
                .OrderBy(x => x.Item.Policy == CachePolicy.Immutable ? 1 : 0) // Don't evict immutable
                .ThenBy(x => x.Metrics?.HitRate ?? 0) // Evict low hit-rate items first
                .ThenBy(x => x.Metrics?.LastAccessTime ?? DateTime.MinValue)
                .ToList();

            int removedCount = 0;
            long bytesFreed = 0;
            long targetSize = (long)(_maxCacheSize * 0.75); // Trim to 75% of max size

            foreach (var item in sortedItems)
            {
                if (_currentCacheSize <= targetSize)
                    break;

                InvalidateCache(item.Key);
                removedCount++;
                bytesFreed += item.Item.Size;
            }

            _logger.LogInformation("Cache trim complete: Removed {Count} items, Freed {FreedMB}MB",
                removedCount, bytesFreed / (1024 * 1024));
        }

        public string GetDependencyChain(string key)
        {
            if (!_cacheDependencies.ContainsKey(key))
                return $"{key} has no dependencies.";

            var sb = new StringBuilder();
            sb.AppendLine($"Dependency chain for {key}:");
            foreach (var dependentKey in _cacheDependencies[key])
            {
                sb.AppendLine($"- {dependentKey}");
            }

            return sb.ToString();
        }

        public async Task StartMaintenanceAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);

                    try
                    {
                        // Check for expired items
                        var expiredCount = 0;
                        foreach (var (key, item) in _cache)
                        {
                            if (item is EnhancedCacheItemWithMetrics<object> cacheItem && cacheItem.IsExpired)
                            {
                                InvalidateCache(key);
                                expiredCount++;
                            }
                        }

                        if (expiredCount > 0)
                        {
                            _logger.LogInformation("Cache maintenance: Removed {Count} expired items", expiredCount);
                        }

                        // Log cache metrics
                        LogCacheMetrics();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during cache maintenance");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Cache maintenance stopped");
            }
        }

        private void LogCacheMetrics()
        {
            var totalItems = _cache.Count;
            var totalHits = _cacheMetrics.Values.Sum(m => m.Hits);
            var totalMisses = _cacheMetrics.Values.Sum(m => m.Misses);
            var hitRate = totalHits + totalMisses > 0 ? (double)totalHits / (totalHits + totalMisses) : 0;

            _logger.LogInformation("Cache metrics - Total items: {TotalItems}, Hit rate: {HitRate:P}, Size: {SizeMB}MB",
                totalItems, hitRate, _currentCacheSize / (1024 * 1024));
        }

        public void Dispose()
        {
            _maintenanceCts?.Cancel();
            _maintenanceCts?.Dispose();
            _cache.Clear();
            _cacheMetrics.Clear();
            _cacheDependencies.Clear();
            _currentCacheSize = 0;
        }

        void ICacheService.AddToCache(string cacheKey, object result, CachePolicy @volatile, IEnumerable<string> enumerable)
        {
            throw new NotImplementedException();
        }

        public void AddToCache(string cacheKey, IEnumerable<Item> items, CachePolicy @volatile)
        {
            throw new NotImplementedException();
        }
    }
}