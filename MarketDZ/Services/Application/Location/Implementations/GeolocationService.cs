using MarketDZ.Models.Firebase;
using MarketDZ.Services.Utils.Firebase;
using Microsoft.Extensions.Logging;
using Firebase.Database;
using Firebase.Database.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Implementations
{
    public class GeoFireService : IGeolocationService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly ILogger<GeoFireService> _logger;
        private const string GeoIndexCollectionName = "items_by_location";

        public GeoFireService(
            FirebaseClient firebaseClient,
            ILogger<GeoFireService> logger)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> CreateGeoIndexedItemAsync(string itemId, double latitude, double longitude)
        {
            try
            {
                var geoIndex = new GeoItemIndex(itemId, latitude, longitude);
                var indexPath = geoIndex.GetIndexPath();

                // Save to Firebase
                await _firebaseClient
                    .Child(indexPath)
                    .PutAsync(geoIndex.ToFirebaseValue());

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating geo-indexed item for ID: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> UpdateGeoIndexedItemAsync(string itemId, double latitude, double longitude)
        {
            try
            {
                // First delete existing indexes for this item
                await DeleteGeoIndexedItemAsync(itemId);

                // Create new index at new location
                return await CreateGeoIndexedItemAsync(itemId, latitude, longitude);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating geo-indexed item for ID: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> DeleteGeoIndexedItemAsync(string itemId)
        {
            try
            {
                // Find all indexes for this item by querying all geohashes
                var indexes = await QueryAllGeoIndexesForItemAsync(itemId);

                // Delete each index
                foreach (var index in indexes)
                {
                    await _firebaseClient
                        .Child(index.GetIndexPath())
                        .DeleteAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting geo-indexed item for ID: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<List<GeoItemIndex>> QueryItemsInRadiusAsync(double centerLat, double centerLon, double radiusKm)
        {
            try
            {
                // Get all geohashes covering the search area
                var geohashes = GetGeohashesInRadius(centerLat, centerLon, radiusKm);
                var items = new List<GeoItemIndex>();

                // Query each geohash
                foreach (var geohash in geohashes)
                {
                    var queryPath = $"{GeoIndexCollectionName}/{geohash}";
                    var snapshot = await _firebaseClient
                        .Child(queryPath)
                        .OnceAsJsonAsync();

                    if (!string.IsNullOrEmpty(snapshot))
                    {
                        var geoItems = DeserializeGeoItems(snapshot, geohash);
                        items.AddRange(geoItems);
                    }
                }

                // Filter by actual distance
                return FilterItemsByDistance(items, centerLat, centerLon, radiusKm,
                    item => (item.Latitude, item.Longitude));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying items in radius");
                return new List<GeoItemIndex>();
            }
        }

        public HashSet<string> GetGeohashesInRadius(double centerLat, double centerLon, double radiusKm, int precision = 5)
        {
            return GeohashUtility.GetGeohashesInRadius(centerLat, centerLon, radiusKm, precision);
        }

        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            return GeohashUtility.CalculateDistance(lat1, lon1, lat2, lon2);
        }

        public string GenerateGeohash(double latitude, double longitude, int precision = 8)
        {
            return GeohashUtility.EncodeGeohash(latitude, longitude, precision);
        }

        public List<T> FilterItemsByDistance<T>(IEnumerable<T> items,
            double centerLat, double centerLon, double radiusKm,
            Func<T, (double Latitude, double Longitude)> coordinateSelector)
        {
            return items.Where(item =>
            {
                var (lat, lon) = coordinateSelector(item);
                var distance = CalculateDistance(centerLat, centerLon, lat, lon);
                return distance <= radiusKm;
            }).ToList();
        }

        private async Task<List<GeoItemIndex>> QueryAllGeoIndexesForItemAsync(string itemId)
        {
            try
            {
                // This is a simplified approach - in production you might want to maintain
                // a reverse index to quickly find all geohashes an item belongs to

                // Query all geohash prefixes
                var snapshot = await _firebaseClient
                    .Child(GeoIndexCollectionName)
                    .OnceAsJsonAsync();

                if (string.IsNullOrEmpty(snapshot))
                    return new List<GeoItemIndex>();

                var result = new List<GeoItemIndex>();

                // Parse and filter for the specific item
                var allIndexes = DeserializeAllGeoItems(snapshot);
                return allIndexes.Where(index => index.ItemId == itemId).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying all geo indexes for item: {ItemId}", itemId);
                return new List<GeoItemIndex>();
            }
        }

        private List<GeoItemIndex> DeserializeGeoItems(string json, string geohash)
        {
            try
            {
                var items = new List<GeoItemIndex>();
                var jsonDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json);

                if (jsonDict != null)
                {
                    foreach (var kvp in jsonDict)
                    {
                        var item = new GeoItemIndex
                        {
                            ItemId = kvp.Key,
                            Geohash = geohash,
                            TargetId = kvp.Key,
                            Latitude = Convert.ToDouble(kvp.Value["latitude"]),
                            Longitude = Convert.ToDouble(kvp.Value["longitude"])
                        };
                        items.Add(item);
                    }
                }

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing geo items");
                return new List<GeoItemIndex>();
            }
        }

        private List<GeoItemIndex> DeserializeAllGeoItems(string json)
        {
            try
            {
                var items = new List<GeoItemIndex>();
                var rootDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>(json);

                if (rootDict != null)
                {
                    foreach (var geohashKvp in rootDict)
                    {
                        foreach (var itemKvp in geohashKvp.Value)
                        {
                            var item = new GeoItemIndex
                            {
                                ItemId = itemKvp.Key,
                                Geohash = geohashKvp.Key,
                                TargetId = itemKvp.Key,
                                Latitude = Convert.ToDouble(itemKvp.Value["latitude"]),
                                Longitude = Convert.ToDouble(itemKvp.Value["longitude"])
                            };
                            items.Add(item);
                        }
                    }
                }

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing all geo items");
                return new List<GeoItemIndex>();
            }
        }

        public Task<Location?> GetCurrentLocation()
        {
            throw new NotImplementedException();
        }

        public Task<string?> GetLocationName(Location location)
        {
            throw new NotImplementedException();
        }

        public Task<Location> GetLocationFromAddress(string searchAddress)
        {
            throw new NotImplementedException();
        }
    }
}