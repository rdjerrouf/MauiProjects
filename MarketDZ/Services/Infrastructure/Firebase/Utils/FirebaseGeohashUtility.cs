using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    /// <summary>
    /// Utility for geospatial operations in Firebase
    /// </summary>
    public class GeohashUtility
    {
        private const string Base32Chars = "0123456789bcdefghjkmnpqrstuvwxyz";
        private const int DefaultPrecision = 8; // ~20m precision

        /// <summary>
        /// Encodes a location to a geohash string
        /// </summary>
        public static string EncodeGeohash(double latitude, double longitude, int precision = DefaultPrecision)
        {
            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };
            var geohash = new char[precision];
            bool isEven = true;
            int bit = 0, charIndex = 0;
            int geohashIndex = 0;

            while (geohashIndex < precision)
            {
                if (isEven)
                {
                    double mid = (lon[0] + lon[1]) / 2;
                    if (longitude > mid)
                    {
                        charIndex = charIndex * 2 + 1;
                        lon[0] = mid;
                    }
                    else
                    {
                        charIndex = charIndex * 2;
                        lon[1] = mid;
                    }
                }
                else
                {
                    double mid = (lat[0] + lat[1]) / 2;
                    if (latitude > mid)
                    {
                        charIndex = charIndex * 2 + 1;
                        lat[0] = mid;
                    }
                    else
                    {
                        charIndex = charIndex * 2;
                        lat[1] = mid;
                    }
                }

                isEven = !isEven;

                if (++bit == 5)
                {
                    geohash[geohashIndex++] = Base32Chars[charIndex];
                    bit = 0;
                    charIndex = 0;
                }
            }

            return new string(geohash);
        }

        /// <summary>
        /// Decodes a geohash string to latitude and longitude
        /// </summary>
        public static (double Latitude, double Longitude) DecodeGeohash(string geohash)
        {
            if (string.IsNullOrEmpty(geohash))
                throw new ArgumentException("Geohash cannot be null or empty", nameof(geohash));

            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };
            bool isEven = true;

            foreach (char c in geohash)
            {
                int charIndex = Base32Chars.IndexOf(c); // Ensure `charIndex` is declared as an int
                if (charIndex == -1)
                    throw new ArgumentException($"Invalid character '{c}' in geohash", nameof(geohash));

                for (int i = 4; i >= 0; i--)
                {
                    int bit = (charIndex >> i) & 1;

                    if (isEven)
                    {
                        double mid = (lon[0] + lon[1]) / 2;
                        lon[bit] = mid;
                    }
                    else
                    {
                        double mid = (lat[0] + lat[1]) / 2;
                        lat[bit] = mid;
                    }

                    isEven = !isEven;
                }
            }

            return ((lat[0] + lat[1]) / 2, (lon[0] + lon[1]) / 2);
        }

        /// <summary>
        /// Gets the neighbors of a geohash
        /// </summary>
        public static string[] GetNeighbors(string geohash)
        {
            var (lat, lon) = DecodeGeohash(geohash);
            double latErr = 90.0 / Math.Pow(2, 5 * geohash.Length / 2);
            double lonErr = 180.0 / Math.Pow(2, 5 * (geohash.Length + 1) / 2);

            var neighbors = new string[8];

            // North
            neighbors[0] = EncodeGeohash(lat + latErr, lon, geohash.Length);
            // Northeast
            neighbors[1] = EncodeGeohash(lat + latErr, lon + lonErr, geohash.Length);
            // East
            neighbors[2] = EncodeGeohash(lat, lon + lonErr, geohash.Length);
            // Southeast
            neighbors[3] = EncodeGeohash(lat - latErr, lon + lonErr, geohash.Length);
            // South
            neighbors[4] = EncodeGeohash(lat - latErr, lon, geohash.Length);
            // Southwest
            neighbors[5] = EncodeGeohash(lat - latErr, lon - lonErr, geohash.Length);
            // West
            neighbors[6] = EncodeGeohash(lat, lon - lonErr, geohash.Length);
            // Northwest
            neighbors[7] = EncodeGeohash(lat + latErr, lon - lonErr, geohash.Length);

            return neighbors;
        }

        /// <summary>
        /// Gets geohashes covering a circular area
        /// </summary>
        public static HashSet<string> GetGeohashesInRadius(double lat, double lon, double radiusKm, int precision = 5)
        {
            // Calculate a rough bounding box
            double latErr = radiusKm / 111.0; // 1 degree of latitude is approx 111km
            double lonErr = radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0));

            // Expand slightly to ensure coverage
            latErr *= 1.1;
            lonErr *= 1.1;

            // Calculate grid dimensions
            int latDivisions = (int)Math.Ceiling(2 * latErr / (90.0 / Math.Pow(2, 5 * precision / 2)));
            int lonDivisions = (int)Math.Ceiling(2 * lonErr / (180.0 / Math.Pow(2, 5 * (precision + 1) / 2)));

            // Generate geohashes
            var geohashes = new HashSet<string>();

            for (int i = 0; i < latDivisions; i++)
            {
                double currLat = lat - latErr + i * 2 * latErr / latDivisions;

                for (int j = 0; j < lonDivisions; j++)
                {
                    double currLon = lon - lonErr + j * 2 * lonErr / lonDivisions;

                    // Calculate actual distance
                    double distance = CalculateDistance(lat, lon, currLat, currLon);

                    if (distance <= radiusKm)
                    {
                        geohashes.Add(EncodeGeohash(currLat, currLon, precision));
                    }
                }
            }

            // Add the center and neighbors to ensure coverage
            string centerHash = EncodeGeohash(lat, lon, precision);
            geohashes.Add(centerHash);
            foreach (var neighbor in GetNeighbors(centerHash))
            {
                geohashes.Add(neighbor);
            }

            return geohashes;
        }

        /// <summary>
        /// Calculates the great-circle distance between two points
        /// </summary>
        public static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double earthRadiusKm = 6371.0;

            // Convert to radians
            lat1 = lat1 * Math.PI / 180.0;
            lon1 = lon1 * Math.PI / 180.0;
            lat2 = lat2 * Math.PI / 180.0;
            lon2 = lon2 * Math.PI / 180.0;

            // Haversine formula
            double dlon = lon2 - lon1;
            double dlat = lat2 - lat1;
            double a = Math.Sin(dlat / 2) * Math.Sin(dlat / 2) +
                       Math.Cos(lat1) * Math.Cos(lat2) *
                       Math.Sin(dlon / 2) * Math.Sin(dlon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * c;
        }
    }

}
