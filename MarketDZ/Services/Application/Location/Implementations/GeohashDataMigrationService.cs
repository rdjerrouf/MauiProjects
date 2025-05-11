using MarketDZ.Models;
using MarketDZ.Services.Items;
using MarketDZ.Services.LocationServices;
using MarketDZ.Services.LocationServices.GeoFire;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
// Add explicit using statement for each Item class to fix ambiguity
using ModelItem = MarketDZ.Models.Item;

namespace MarketDZ.Services.Application.Location.Implementations
{
    public class GeohashDataMigrationService : IGeohashDataMigrationService
    {
        private readonly IItemCoreService _itemCoreService;
        private readonly IGeoFireService _geoFireService;
        private readonly ILogger<GeohashDataMigrationService> _logger;

        public GeohashDataMigrationService(
            IItemCoreService itemCoreService,
            IGeoFireService geoFireService,
            ILogger<GeohashDataMigrationService> logger)
        {
            _itemCoreService = itemCoreService ?? throw new ArgumentNullException(nameof(itemCoreService));
            _geoFireService = geoFireService ?? throw new ArgumentNullException(nameof(geoFireService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MigrationResult> MigrateExistingGeohashDataAsync()
        {
            var result = new MigrationResult();

            try
            {
                _logger.LogInformation("Starting geohash data migration");

                // Get all items with location data
                var allItems = await _itemCoreService.GetAllItemsAsync();

                // Cast the objects to Items during the filtering
                var itemsWithLocation = allItems
                    .Where(obj =>
                    {
                        if (obj is ModelItem item)
                            return item.Latitude.HasValue && item.Longitude.HasValue;
                        return false;
                    })
                    .Cast<ModelItem>()
                    .ToList();

                result.TotalItemsFound = itemsWithLocation.Count();
                _logger.LogInformation($"Found {result.TotalItemsFound} items with location data");

                foreach (var item in itemsWithLocation)
                {
                    try
                    {
                        var success = await _geoFireService.CreateGeoIndexedItemAsync(
                            item.Id.ToString(),
                            item.Latitude!.Value,
                            item.Longitude!.Value);

                        if (success)
                        {
                            result.SuccessfulMigrations++;
                        }
                        else
                        {
                            result.FailedMigrations++;
                            result.Errors.Add($"Failed to migrate item {item.Id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailedMigrations++;
                        result.Errors.Add($"Error migrating item {item.Id}: {ex.Message}");
                        _logger.LogError(ex, $"Error migrating item {item.Id}");
                    }
                }

                result.IsCompleted = true;
                result.CompletionTime = DateTime.UtcNow;

                _logger.LogInformation($"Migration completed. Success: {result.SuccessfulMigrations}, Failed: {result.FailedMigrations}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during geohash data migration");
                result.Errors.Add($"Migration process error: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> ValidateMigrationAsync()
        {
            try
            {
                // Get all items with location data
                var allItems = await _itemCoreService.GetAllItemsAsync();

                // Cast the objects to Items during the filtering
                var itemsWithLocation = allItems
                    .Where(obj =>
                    {
                        if (obj is ModelItem item)
                            return item.Latitude.HasValue && item.Longitude.HasValue;
                        return false;
                    })
                    .Cast<ModelItem>()
                    .ToList();

                foreach (var item in itemsWithLocation)
                {
                    // Check if item has a geo index
                    var geoItems = await _geoFireService.QueryItemsInRadiusAsync(
                        item.Latitude!.Value,
                        item.Longitude!.Value,
                        0.01); // Very small radius to find exact match

                    if (!geoItems.Any(g => g.ItemId == item.Id.ToString()))
                    {
                        _logger.LogWarning($"Item {item.Id} is missing geo index");
                        return false;
                    }
                }

                _logger.LogInformation("Migration validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating migration");
                return false;
            }
        }
    }

    public class MigrationResult
    {
        public bool IsCompleted { get; set; }
        public int TotalItemsFound { get; set; }
        public int SuccessfulMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public DateTime CompletionTime { get; set; }
    }
}
