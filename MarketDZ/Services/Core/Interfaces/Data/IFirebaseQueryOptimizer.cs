using MarketDZ.Models;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Firebase.Base.Adapters;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    // Add this extension method for FilterParameters
    public static class FilterParametersExtensions
    {
        public static string GetCacheKey(this FilterParameters filter)
        {
            return $"{filter.UserId}_{filter.Category}_{filter.State}_{filter.Status}_{filter.MinPrice}_{filter.MaxPrice}_{filter.SearchTerm}_{filter.SortBy}";
        }
    }

    // Add this extension method for collection batching
    public static class EnumerableExtensions
    {
        public static IEnumerable<IEnumerable<T>> Batch<T>(this IEnumerable<T> source, int batchSize)
        {
            var batch = new List<T>(batchSize);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count == batchSize)
                {
                    yield return batch;
                    batch = new List<T>(batchSize);
                }
            }

            if (batch.Any())
            {
                yield return batch;
            }
        }
    }

    public interface IFirebaseQueryOptimizer
    {
        Task<List<Item>> OptimizeQueryAsync(FilterParameters filter);
        Task<PaginatedResult<Item>> OptimizePaginatedQueryAsync(FilterParameters filter);
        Task<QueryPlan> GenerateQueryPlanAsync(FilterParameters filter);
        Task<QueryPerformanceReport> BenchmarkQueryAsync(FilterParameters filter, int iterations = 5);
    }

    public class FirebaseQueryOptimizer : IFirebaseQueryOptimizer
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseQueryOptimizer> _logger;
        private readonly QueryPlanCache _queryPlanCache;

        // Index path constants
        private const string ItemsByUserPath = "items_by_user";
        private const string ItemsByCategoryPath = "items_by_category";
        private const string ItemsByStatePath = "items_by_state";
        private const string ItemsByStatusPath = "items_by_status";
        private const string ItemsByLocationPath = "items_by_location";
        private const string ItemsByPricePath = "items_by_price";

        public FirebaseQueryOptimizer(IAppCoreDataStore dataStore, ILogger<FirebaseQueryOptimizer> logger)
        {
            _dataStore = dataStore;
            _logger = logger;
            _queryPlanCache = new QueryPlanCache(TimeSpan.FromMinutes(5));
        }

        public async Task<List<Item>> OptimizeQueryAsync(FilterParameters filter)
        {
            var plan = await GenerateQueryPlanAsync(filter);
            _logger.LogInformation($"Executing query plan: {plan}");

            try
            {
                // Execute index-based query if available
                if (plan.PrimaryIndexPath != null)
                {
                    var indexResults = await QueryUsingIndexAsync(plan.PrimaryIndexPath, filter);
                    return ApplyClientSideFilters(indexResults, filter, plan);
                }

                // Fallback to full collection scan
                var allItems = await _dataStore.GetCollectionAsync<FirebaseItem>("items");
                var convertedItems = allItems.Select(fi => fi.ToItem()).ToList();
                return ApplyClientSideFilters(convertedItems, filter, plan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query optimization failed");
                throw;
            }
        }

        public async Task<PaginatedResult<Item>> OptimizePaginatedQueryAsync(FilterParameters filter)
        {
            var plan = await GenerateQueryPlanAsync(filter);
            _logger.LogInformation($"Executing paginated query plan: {plan}");

            try
            {
                var pageSize = filter.Take > 0 ? filter.Take : 20;

                if (plan.PrimaryIndexPath != null)
                {
                    return await GetPaginatedUsingIndexAsync(plan.PrimaryIndexPath, filter, pageSize, plan);
                }

                return await GetPaginatedFullCollectionAsync(filter, pageSize, plan);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Paginated query optimization failed");
                throw;
            }
        }

        public async Task<QueryPlan> GenerateQueryPlanAsync(FilterParameters filter)
        {
            // Check cache first
            var cacheKey = filter.GetCacheKey();
            if (_queryPlanCache.TryGet(cacheKey, out QueryPlan cachedPlan))
            {
                return cachedPlan;
            }

            var plan = new QueryPlan
            {
                FilterParameters = filter,
                GeneratedAt = DateTime.UtcNow
            };

            // Analyze filter parameters to determine optimal strategy
            if (!string.IsNullOrEmpty(filter.UserId))
            {
                plan.PrimaryIndexPath = $"{ItemsByUserPath}/{filter.UserId}";
                plan.Strategy = QueryStrategy.UserIndex;
            }

            else if (!string.IsNullOrEmpty(filter.Category))
            {
                plan.PrimaryIndexPath = $"{ItemsByCategoryPath}/{filter.Category}";
                plan.Strategy = QueryStrategy.CategoryIndex;

                // Add secondary index if available
                if (filter.State.HasValue)
                {
                    plan.SecondaryIndexPath = $"{ItemsByStatePath}/{filter.State.Value}";
                }
            }
            else if (filter.State.HasValue)
            {
                plan.PrimaryIndexPath = $"{ItemsByStatePath}/{filter.State.Value}";
                plan.Strategy = QueryStrategy.StateIndex;
            }
            else if (filter.Status.HasValue)
            {
                plan.PrimaryIndexPath = $"{ItemsByStatusPath}/{filter.Status.Value}";
                plan.Strategy = QueryStrategy.StatusIndex;
            }
            else if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue)
            {
                var minBucket = GetPriceBucket(filter.MinPrice ?? 0);
                plan.PrimaryIndexPath = $"{ItemsByPricePath}/{minBucket}";
                plan.Strategy = QueryStrategy.PriceRangeIndex;
            }
            else
            {
                plan.Strategy = QueryStrategy.FullScan;
            }

            // Estimate expected result size
            if (plan.PrimaryIndexPath != null)
            {
                plan.EstimatedResultSize = await EstimateIndexSizeAsync(plan.PrimaryIndexPath);
            }
            else
            {
                plan.EstimatedResultSize = await _dataStore.GetCollectionSizeAsync("items");
            }

            // Cache the plan
            _queryPlanCache.Add(cacheKey, plan);
            return plan;
        }

        public async Task<QueryPerformanceReport> BenchmarkQueryAsync(FilterParameters filter, int iterations = 5)
        {
            var report = new QueryPerformanceReport
            {
                FilterParameters = filter,
                TestDate = DateTime.UtcNow,
                Iterations = iterations
            };

            // Warm-up
            await OptimizeQueryAsync(filter);

            // Run benchmarks
            var timings = new List<long>();
            for (int i = 0; i < iterations; i++)
            {
                var sw = Stopwatch.StartNew();
                await OptimizeQueryAsync(filter);
                sw.Stop();
                timings.Add(sw.ElapsedMilliseconds);
            }

            report.AverageTimeMs = timings.Average();
            report.MinTimeMs = timings.Min();
            report.MaxTimeMs = timings.Max();
            report.P90TimeMs = CalculatePercentile(timings, 0.9);

            // Generate query plan for analysis
            report.QueryPlan = await GenerateQueryPlanAsync(filter);

            return report;
        }

        #region Private Implementation

        private async Task<int> EstimateIndexSizeAsync(string indexPath)
        {
            try
            {
                var indexData = await _dataStore.GetCollectionAsync<IndexEntry>(indexPath);
                return indexData?.Count() ?? 0;  // Use Count() method instead of property
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<Item>> QueryUsingIndexAsync(string indexPath, FilterParameters filter)
        {
            var itemEntries = await _dataStore.GetCollectionAsync<IndexEntry>(indexPath);
            if (itemEntries == null || !itemEntries.Any())
                return new List<Item>();

            // Parallel fetch optimization for large result sets
            var batchSize = Math.Min(50, itemEntries.Count());
            var batches = itemEntries.Batch(batchSize);

            var results = new List<Item>();
            foreach (var batch in batches)
            {
                var batchTasks = batch.Select(async entry =>
                {
                    var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"items/{entry.Id}");
                    return firebaseItem?.ToItem();
                });

                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults.Where(item => item != null));
            }

            return results;
        }

        private List<Item> ApplyClientSideFilters(List<Item> items, FilterParameters filter, QueryPlan plan)
        {
            var query = items.AsQueryable();

            // Apply only the filters not handled by indexes
            if (!plan.FilterParametersHandled.Contains(nameof(filter.MinPrice)) && filter.MinPrice.HasValue)
                query = query.Where(i => i.Price >= filter.MinPrice.Value);

            if (!plan.FilterParametersHandled.Contains(nameof(filter.MaxPrice)) && filter.MaxPrice.HasValue)
                query = query.Where(i => i.Price <= filter.MaxPrice.Value);

            if (!string.IsNullOrEmpty(filter.SearchTerm))
                query = query.Where(i =>
                    i.Title.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    i.Description.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase));

            // Sorting
            query = ApplySorting(query, filter);

            return query.ToList();
        }

        private IQueryable<Item> ApplySorting(IQueryable<Item> query, FilterParameters filter)
        {
            if (filter.SortBy == SortOption.PriceLowToHigh)
                return query.OrderBy(i => i.Price);

            if (filter.SortBy == SortOption.PriceHighToLow)
                return query.OrderByDescending(i => i.Price);

            if (filter.SortBy == SortOption.DateNewest)
                return query.OrderByDescending(i => i.ListedDate);

            if (filter.SortBy == SortOption.DateOldest)
                return query.OrderBy(i => i.ListedDate);

            return query; // Default case if no sorting is specified
        }

        private async Task<PaginatedResult<Item>> GetPaginatedUsingIndexAsync(
            string indexPath,
            FilterParameters filter,
            int pageSize,
            QueryPlan plan)
        {
            var queryParams = new QueryParameters
            {
                Skip = filter.Skip,
                Take = pageSize + 1 // Extra to check for next page
            };

            var itemEntries = await _dataStore.GetCollectionAsync<IndexEntry>(indexPath, queryParams);
            if (itemEntries == null || !itemEntries.Any())
                return CreateEmptyPaginatedResult(pageSize);

            // Get items in parallel
            var itemTasks = itemEntries
                .Take(pageSize)
                .Select(async entry =>
                {
                    var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"items/{entry.Id}");
                    return firebaseItem?.ToItem();
                });

            var items = (await Task.WhenAll(itemTasks)).Where(item => item != null).ToList();

            // Apply remaining filters
            items = ApplyClientSideFilters(items, filter, plan);

            return new PaginatedResult<Item>
            {
                Items = items,
                TotalItems = items.Count,
                Page = (filter.Skip / pageSize) + 1,
                PageSize = pageSize,
                HasNextPage = itemEntries.Count() > pageSize
            };
        }

        private async Task<PaginatedResult<Item>> GetPaginatedFullCollectionAsync(
            FilterParameters filter,
            int pageSize,
            QueryPlan plan)
        {
            var allItems = (await _dataStore.GetCollectionAsync<FirebaseItem>("items"))
                .Select(fi => fi.ToItem())
                .ToList();

            var filteredItems = ApplyClientSideFilters(allItems, filter, plan);
            var pagedItems = filteredItems
                .Skip(filter.Skip)
                .Take(pageSize)
                .ToList();

            int totalPages = (int)Math.Ceiling(filteredItems.Count / (double)pageSize);

            return new PaginatedResult<Item>
            {
                Items = pagedItems,
                TotalItems = filteredItems.Count,
                Page = (filter.Skip / pageSize) + 1,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = (filter.Skip + pageSize) < filteredItems.Count
            };
        }

        private string GetPriceBucket(decimal price)
        {
            if (price < 10) return "0-10";
            if (price < 50) return "10-50";
            if (price < 100) return "50-100";
            if (price < 500) return "100-500";
            if (price < 1000) return "500-1000";
            if (price < 5000) return "1000-5000";
            return "5000+";
        }

        private double CalculatePercentile(List<long> values, double percentile)
        {
            if (!values.Any()) return 0;
            values.Sort();
            int index = (int)Math.Ceiling(percentile * values.Count) - 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        private PaginatedResult<Item> CreateEmptyPaginatedResult(int pageSize)
        {
            return new PaginatedResult<Item>
            {
                Items = new List<Item>(),
                TotalItems = 0,
                Page = 1,
                PageSize = pageSize,
                TotalPages = 0,
                HasNextPage = false
            };
        }

        #endregion
    }

    // Add this class to handle Firebase index entries
    public class IndexEntry
    {
        public string Id { get; set; }
    }

    public enum QueryStrategy
    {
        UserIndex,
        CategoryIndex,
        StateIndex,
        StatusIndex,
        PriceRangeIndex,
        FullScan,
        CompositeIndex
    }

    public class QueryPlan
    {
        public FilterParameters FilterParameters { get; set; }
        public QueryStrategy Strategy { get; set; }
        public string PrimaryIndexPath { get; set; }
        public string SecondaryIndexPath { get; set; }
        public int EstimatedResultSize { get; set; }
        public DateTime GeneratedAt { get; set; }
        public HashSet<string> FilterParametersHandled { get; } = new HashSet<string>();

        public override string ToString()
        {
            return $"{Strategy} using {PrimaryIndexPath ?? "no index"} (est. {EstimatedResultSize} items)";
        }
    }

    public class QueryPerformanceReport
    {
        public FilterParameters FilterParameters { get; set; }
        public QueryPlan QueryPlan { get; set; }
        public DateTime TestDate { get; set; }
        public int Iterations { get; set; }
        public double AverageTimeMs { get; set; }
        public double MinTimeMs { get; set; }
        public double MaxTimeMs { get; set; }
        public double P90TimeMs { get; set; }

        public override string ToString()
        {
            return $"Query executed in avg {AverageTimeMs}ms (min: {MinTimeMs}ms, max: {MaxTimeMs}ms) using {QueryPlan.Strategy}";
        }
    }

    internal class QueryPlanCache
    {
        private readonly Dictionary<string, (QueryPlan plan, DateTime expiry)> _cache = new Dictionary<string, (QueryPlan, DateTime)>();
        private readonly TimeSpan _cacheDuration;

        public QueryPlanCache(TimeSpan cacheDuration)
        {
            _cacheDuration = cacheDuration;
        }

        public bool TryGet(string key, out QueryPlan plan)
        {
            CleanExpired();
            if (_cache.TryGetValue(key, out var entry))
            {
                plan = entry.plan;
                return true;
            }
            plan = null;
            return false;
        }

        public void Add(string key, QueryPlan plan)
        {
            _cache[key] = (plan, DateTime.UtcNow.Add(_cacheDuration));
            CleanExpired();
        }

        private void CleanExpired()
        {
            var expiredKeys = _cache.Where(kvp => kvp.Value.expiry <= DateTime.UtcNow)
                                  .Select(kvp => kvp.Key)
                                  .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.Remove(key);
            }
        }
    }
}