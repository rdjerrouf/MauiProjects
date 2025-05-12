using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Core.ValueObjects;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;
using Location = MarketDZ.Models.Core.ValueObjects.Location;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase implementation of the extended item repository
    /// </summary>
    public class FirebaseExtendedItemRepository : FirebaseItemRepository, IExtendedItemRepository
    {
        private readonly IFirebaseIndexManager _indexManager;

        public FirebaseExtendedItemRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<Item, FirebaseItem> entityMapper,
            ILogger<FirebaseExtendedItemRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper,
            IFirebaseIndexManager indexManager,
            IFirebaseQueryOptimizer queryOptimizer)
            : base(dataStore, entityMapper, logger, cacheService, transactionHelper, indexManager, queryOptimizer)
        {
            _indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
        }

        /// <summary>
        /// Get items near a specific location
        /// </summary>
        public async Task<IEnumerable<Item>> GetNearbyAsync(double latitude, double longitude, double radiusKm)
        {
            string cacheKey = $"items_nearby_{latitude:F6}_{longitude:F6}_{radiusKm}";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<IEnumerable<Item>>(cacheKey, out var cachedItems))
            {
                return cachedItems;
            }

            try
            {
                // Create location bounds
                var center = new Location(latitude, longitude);
                var bounds = Models.Core.ValueObjects.LocationExtensions.CreateBounds(center, radiusKm * 1000); // Convert km to meters

                // Get items within the bounds using geohash index
                var filters = new FilterParameters
                {
                    Status = ItemStatus.Active,
                    SortByDistance = true,
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusKm = radiusKm
                };

                var items = await GetFilteredAsync(filters);

                // Further filter by exact distance and sort
                var nearbyItems = items
                    .Where(item =>
                    {
                        if (!item.Latitude.HasValue || !item.Longitude.HasValue)
                            return false;

                        var distance = Location.CalculateDistance(
                            latitude, longitude,
                            item.Latitude.Value, item.Longitude.Value) / 1000; // Convert to km

                        return distance <= radiusKm;
                    })
                    .OrderBy(item =>
                    {
                        if (!item.Latitude.HasValue || !item.Longitude.HasValue)
                            return double.MaxValue;

                        return Location.CalculateDistance(
                            latitude, longitude,
                            item.Latitude.Value, item.Longitude.Value);
                    })
                    .ToList();

                // Cache the result
                CacheService?.AddToCache(cacheKey, nearbyItems, TimeSpan.FromMinutes(5));

                return nearbyItems;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting items near location ({latitude}, {longitude}) within {radiusKm}km");
                return Enumerable.Empty<Item>();
            }
        }

        /// <summary>
        /// Get paginated items using query parameters
        /// </summary>
        public async Task<PaginatedResult<Item>> GetPaginatedAsync(ItemQueryParameters parameters)
        {
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            try
            {
                // Convert ItemQueryParameters to FilterParameters
                var filters = new FilterParameters
                {
                    Skip = parameters.Skip,
                    Take = parameters.Take
                };

                // Apply filters from query parameters
                foreach (var filter in parameters.FilterCriteria)
                {
                    switch (filter.Field.ToLowerInvariant())
                    {
                        case "category":
                            filters.Category = filter.Value?.ToString();
                            break;
                        case "status":
                            if (Enum.TryParse<ItemStatus>(filter.Value?.ToString(), out var status))
                                filters.Status = status;
                            break;
                        case "state":
                            if (Enum.TryParse<AlState>(filter.Value?.ToString(), out var state))
                                filters.State = state;
                            break;
                        case "minprice":
                            if (decimal.TryParse(filter.Value?.ToString(), out var minPrice))
                                filters.MinPrice = minPrice;
                            break;
                        case "maxprice":
                            if (decimal.TryParse(filter.Value?.ToString(), out var maxPrice))
                                filters.MaxPrice = maxPrice;
                            break;
                        case "searchtext":
                            filters.SearchText = filter.Value?.ToString();
                            break;
                        case "postedbyuserid":
                        case "userid":
                            filters.UserId = filter.Value?.ToString();
                            break;
                    }
                }

                // Apply sorting from query parameters
                if (parameters.SortCriteria.Any())
                {
                    var firstSort = parameters.SortCriteria.First();
                    switch (firstSort.Field.ToLowerInvariant())
                    {
                        case "price":
                            filters.SortBy = firstSort.Direction == Models.Core.Enums.SortDirection.Ascending
                                ? SortOption.PriceLowToHigh
                                : SortOption.PriceHighToLow;
                            break;
                        case "listeddate":
                        case "date":
                            filters.SortBy = firstSort.Direction == Models.Core.Enums.SortDirection.Ascending
                                ? SortOption.DateOldest
                                : SortOption.DateNewest;
                            break;
                    }
                }

                return await GetPaginatedAsync(filters);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting paginated items with query parameters");
                // Create PaginatedResult using the existing constructor
                return new PaginatedResult<Item>(new List<Item>(), 0, 1, parameters.Take);
            }
        }
    }
}