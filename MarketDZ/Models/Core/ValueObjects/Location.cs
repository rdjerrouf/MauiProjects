// Location classes and supporting types

namespace MarketDZ.Models.Core.ValueObjects // Changed namespace to avoid conflict
{
    /// <summary>
    /// Represents a geographic location with latitude and longitude
    /// </summary>
    public class Location
    {
        /// <summary>
        /// Latitude coordinate (-90 to 90)
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude coordinate (-180 to 180)
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Optional accuracy in meters
        /// </summary>
        public double? Accuracy { get; set; }

        /// <summary>
        /// Optional altitude in meters
        /// </summary>
        public double? Altitude { get; set; }

        /// <summary>
        /// Optional speed in meters per second
        /// </summary>
        public double? Speed { get; set; }

        /// <summary>
        /// Optional course/heading in degrees (0-360)
        /// </summary>
        public double? Course { get; set; }

        /// <summary>
        /// When this location was recorded
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Creates a new Location with the specified coordinates
        /// </summary>
        public Location(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Default constructor for serialization
        /// </summary>
        public Location()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a Location from decimal degree coordinates
        /// </summary>
        public static Location FromCoordinates(double latitude, double longitude, double? accuracy = null)
        {
            return new Location
            {
                Latitude = latitude,
                Longitude = longitude,
                Accuracy = accuracy,
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Calculates the distance to another location in meters using the Haversine formula
        /// </summary>
        public double DistanceTo(Location other)
        {
            if (other == null)
                return double.MaxValue;

            return CalculateDistance(this.Latitude, this.Longitude, other.Latitude, other.Longitude);
        }

        /// <summary>
        /// Calculates the distance between two points in meters using the Haversine formula
        /// </summary>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000; // Earth's radius in meters
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return R * c;
        }

        /// <summary>
        /// Validates if the coordinates are within valid ranges
        /// </summary>
        public bool IsValid()
        {
            return Latitude >= -90 && Latitude <= 90 &&
                   Longitude >= -180 && Longitude <= 180;
        }

        /// <summary>
        /// Returns a string representation of the location
        /// </summary>
        public override string ToString()
        {
            return $"({Latitude:F6}, {Longitude:F6})";
        }

        /// <summary>
        /// Equality comparison
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is not Location other)
                return false;

            return Math.Abs(Latitude - other.Latitude) < 0.000001 &&
                   Math.Abs(Longitude - other.Longitude) < 0.000001;
        }

        /// <summary>
        /// Get hash code
        /// </summary>
        public override int GetHashCode()
        {
            return HashCode.Combine(Latitude, Longitude);
        }

        /// <summary>
        /// Convert degrees to radians
        /// </summary>
        private static double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        /// <summary>
        /// Convert radians to degrees
        /// </summary>
        private static double ToDegrees(double radians)
        {
            return radians * (180 / Math.PI);
        }
    }

    /// <summary>
    /// Extension methods for Location
    /// </summary>
    public static class LocationExtensions
    {
        /// <summary>
        /// Creates a bounding box around a center point
        /// </summary>
        public static LocationBounds CreateBounds(this Location center, double radiusInMeters)
        {
            const double earthRadius = 6371000; // meters

            var lat = center.Latitude;
            var lon = center.Longitude;

            // Convert radius to degrees
            var latDelta = (radiusInMeters / earthRadius) * (180 / Math.PI);
            var lonDelta = (radiusInMeters / earthRadius) * (180 / Math.PI) / Math.Cos(lat * Math.PI / 180);

            return new LocationBounds
            {
                NorthLatitude = lat + latDelta,
                SouthLatitude = lat - latDelta,
                EastLongitude = lon + lonDelta,
                WestLongitude = lon - lonDelta
            };
        }

        /// <summary>
        /// Checks if this location is within the specified bounds
        /// </summary>
        public static bool IsWithinBounds(this Location location, LocationBounds bounds)
        {
            return location.Latitude >= bounds.SouthLatitude &&
                   location.Latitude <= bounds.NorthLatitude &&
                   location.Longitude >= bounds.WestLongitude &&
                   location.Longitude <= bounds.EastLongitude;
        }
    }

    /// <summary>
    /// Represents a rectangular area defined by geographic coordinates
    /// </summary>
    public class LocationBounds
    {
        public double NorthLatitude { get; set; }
        public double SouthLatitude { get; set; }
        public double EastLongitude { get; set; }
        public double WestLongitude { get; set; }

        /// <summary>
        /// Gets the center point of the bounds
        /// </summary>
        public Location GetCenter()
        {
            var centerLat = (NorthLatitude + SouthLatitude) / 2;
            var centerLon = (EastLongitude + WestLongitude) / 2;
            return new Location(centerLat, centerLon);
        }

        /// <summary>
        /// Gets the diagonal distance across the bounds in meters
        /// </summary>
        public double GetDiagonalDistance()
        {
            var northEast = new Location(NorthLatitude, EastLongitude);
            var southWest = new Location(SouthLatitude, WestLongitude);
            return northEast.DistanceTo(southWest);
        }
    }
}

