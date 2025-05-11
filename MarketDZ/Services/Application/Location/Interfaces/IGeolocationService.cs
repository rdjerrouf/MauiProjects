using MarketDZ.Models.Firebase;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Location.Interfaces
{
    /// <summary>
    /// Interface for geospatial operations using geohash for Firebase integration
    /// </summary>
    public interface IGeolocationService
    {
        /// <summary>
        /// Creates a geo-indexed item in Firebase
        /// </summary>
        Task<bool> CreateGeoIndexedItemAsync(string itemId, double latitude, double longitude);

        /// <summary>
        /// Updates the location of an existing geo-indexed item
        /// </summary>
        Task<bool> UpdateGeoIndexedItemAsync(string itemId, double latitude, double longitude);

        /// <summary>
        /// Deletes a geo-indexed item from Firebase
        /// </summary>
        Task<bool> DeleteGeoIndexedItemAsync(string itemId);

        /// <summary>
        /// Queries items within a radius from a center point
        /// </summary>
        Task<List<GeoItemIndex>> QueryItemsInRadiusAsync(double centerLat, double centerLon, double radiusKm);

        /// <summary>
        /// Gets all geohashes that cover a circular area
        /// </summary>
        HashSet<string> GetGeohashesInRadius(double centerLat, double centerLon, double radiusKm, int precision = 5);

        /// <summary>
        /// Calculates distance between two points in kilometers
        /// </summary>
        double CalculateDistance(double lat1, double lon1, double lat2, double lon2);

        /// <summary>
        /// Generates a geohash from coordinates
        /// </summary>
        string GenerateGeohash(double latitude, double longitude, int precision = 8);

        /// <summary>
        /// Filters items by distance from a point
        /// </summary>
        List<T> FilterItemsByDistance<T>(IEnumerable<T> items,
            double centerLat, double centerLon, double radiusKm,
            System.Func<T, (double Latitude, double Longitude)> coordinateSelector);
        Task<Location?> GetCurrentLocation();
        Task<string?> GetLocationName(Location location);
        Task<Location> GetLocationFromAddress(string searchAddress);
    }
}