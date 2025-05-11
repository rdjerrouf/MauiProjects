using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Implementations
{
    public class GeoFireService : IGeoFireService
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
                // Create a geohash for this location
                string geohash = GenerateGeohash(latitude, longitude);

                // Create the data structure for Firebase
                var geoData = new
                {
                    latitude = latitude,
                    longitude = longitude
                };

                // Path in Firebase where data will be stored
                string path = $"{GeoIndexCollectionName}/{geohash}/{itemId}";

                // Save to Firebase
                await _firebaseClient
                    .Child(path)
                    .PutAsync(geoData);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating geo-indexed item for ID: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> DeleteGeoIndexedItemAsync(string itemId)
        {
            try
            {
                // Since we don't know the geohash without querying,
                // we need to query all geohashes to find this item
                var allGeohashPaths = await _firebaseClient
                    .Child(GeoIndexCollectionName)
                    .OnceAsJsonAsync();

                if (string.IsNullOrEmpty(allGeohashPaths))
                    return true; // Nothing to delete

                // Parse the JSON to look for the item in all geohashes
                var geohashNodes = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(allGeohashPaths);

                if (geohashNodes == null)
                    return true;

                // Find all geohashes containing this item
                foreach (var geohashKvp in geohashNodes)
                {
                    string geohash = geohashKvp.Key;
                    var itemsInGeohash = geohashKvp.Value;

                    if (itemsInGeohash.ContainsKey(itemId))
                    {
                        // Found the item in this geohash, delete it
                        await _firebaseClient
                            .Child($"{GeoIndexCollectionName}/{geohash}/{itemId}")
                            .DeleteAsync();
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting geo-indexed item for ID: {ItemId}", itemId);
                return false;
            }
        }

        public async Task<List<GeoIndexedItem>> QueryItemsInRadiusAsync(double latitude, double longitude, double radiusKm)
        {
            try
            {
                // Get geohashes covering the search area
                var geohashes = GetGeohashesInRadius(latitude, longitude, radiusKm);
                var result = new List<GeoIndexedItem>();

                // Query each geohash
                foreach (var geohash in geohashes)
                {
                    var queryPath = $"{GeoIndexCollectionName}/{geohash}";
                    var snapshot = await _firebaseClient
                        .Child(queryPath)
                        .OnceAsJsonAsync();

                    if (!string.IsNullOrEmpty(snapshot))
                    {
                        var items = DeserializeGeoItems(snapshot);
                        result.AddRange(items);
                    }
                }

                // Filter by actual distance
                return result.Where(item =>
                    CalculateDistance(latitude, longitude, item.Latitude, item.Longitude) <= radiusKm)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error querying items in radius");
                return new List<GeoIndexedItem>();
            }
        }

        public List<Item> FilterItemsByDistance(ObservableCollection<Item> items, double latitude, double longitude, double radiusKm, Func<Item, (double, double)> locationSelector)
        {
            // Convert ObservableCollection to List before processing
            var itemsList = items.ToList();

            return itemsList
                .Where(item =>
                {
                    var (itemLat, itemLong) = locationSelector(item);
                    return CalculateDistance(latitude, longitude, itemLat, itemLong) <= radiusKm;
                })
                .ToList();
        }

        public double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Haversine formula to calculate the distance between two points on the Earth
            const double EarthRadiusKm = 6371;

            double dLat = DegreesToRadians(lat2 - lat1);
            double dLon = DegreesToRadians(lon2 - lon1);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return EarthRadiusKm * c;
        }

        private double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180;
        }

        #region Geohash Helper Methods

        private HashSet<string> GetGeohashesInRadius(double latitude, double longitude, double radiusKm, int precision = 5)
        {
            // This should be implemented similar to GeohashUtility.GetGeohashesInRadius
            // Returning a simplified implementation for now
            var result = new HashSet<string>();

            // Central geohash
            string centerGeohash = GenerateGeohash(latitude, longitude, precision);
            result.Add(centerGeohash);

            // Add neighboring geohashes (this is simplified - a production implementation 
            // would calculate all geohashes that actually intersect with the radius)
            var neighbors = GetGeohashNeighbors(centerGeohash);
            foreach (var neighbor in neighbors)
            {
                result.Add(neighbor);
            }

            return result;
        }

        private string GenerateGeohash(double latitude, double longitude, int precision = 5)
        {
            // Simplified implementation - would need a complete geohash implementation
            // For production code, use a proper geohash library or implement the algorithm
            const string BASE32 = "0123456789bcdefghjkmnpqrstuvwxyz";

            // This is a placeholder - in production, implement actual geohashing
            string hash = "";
            var random = new Random((int)(latitude * 1000000 + longitude * 1000));

            for (int i = 0; i < precision; i++)
            {
                hash += BASE32[random.Next(BASE32.Length)];
            }

            return hash;
        }

        private string[] GetGeohashNeighbors(string geohash)
        {
            // Simplified implementation - would calculate actual neighbors in production
            // For a proper implementation, see the GeohashUtility class
            return new string[8]; // Placeholder - should return actual neighbors
        }

        private List<GeoIndexedItem> DeserializeGeoItems(string json)
        {
            try
            {
                var items = new List<GeoIndexedItem>();
                var jsonDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json);

                if (jsonDict != null)
                {
                    foreach (var kvp in jsonDict)
                    {
                        var item = new GeoIndexedItem
                        {
                            ItemId = kvp.Key,
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
                return new List<GeoIndexedItem>();
            }
        }

        #endregion
    }
}