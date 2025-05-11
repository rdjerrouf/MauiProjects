using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Infrastructure.Firebase.Indexes
{
    /// <summary>
    /// Geohash-to-Item index entry for proximity searches
    /// </summary>
    public class GeoItemIndex : FirebaseIndex
    {
        /// <summary>
        /// The geohash value (precision based on zoom level)
        /// </summary>
        public string Geohash { get; set; } = string.Empty;

        /// <summary>
        /// The item ID
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Item's latitude
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Item's longitude
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Creates a new geo-item index entry
        /// </summary>
        public GeoItemIndex() { }

        /// <summary>
        /// Creates a new geo-item index entry
        /// </summary>
        public GeoItemIndex(string itemId, double latitude, double longitude)
        {
            ItemId = itemId;
            TargetId = itemId;
            Latitude = latitude;
            Longitude = longitude;
            Geohash = GenerateGeohash(latitude, longitude, 8); // 8 character precision (~20m)
        }

        /// <summary>
        /// Generates the path for this index in Firebase
        /// </summary>
        public string GetIndexPath()
        {
            return $"items_by_location/{Geohash}/{ItemId}";
        }

        /// <summary>
        /// Converts to a Firebase-compatible value
        /// </summary>
        public override object ToFirebaseValue()
        {
            return new Dictionary<string, object>
            {
                ["latitude"] = Latitude,
                ["longitude"] = Longitude
            };
        }

        /// <summary>
        /// Generate a geohash from latitude and longitude
        /// </summary>
        /// <remarks>
        /// Simplified geohash implementation - in production you might want 
        /// to use a dedicated geohash library with more features
        /// </remarks>
        private static string GenerateGeohash(double latitude, double longitude, int precision)
        {
            const string base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };
            char[] geohash = new char[precision];
            bool isEven = true;

            int bit = 0, ch = 0;
            while (geohash.Length < precision)
            {
                double mid;
                if (isEven)
                {
                    mid = (lon[0] + lon[1]) / 2;
                    if (longitude > mid)
                    {
                        ch |= (1 << (4 - bit));
                        lon[0] = mid;
                    }
                    else
                    {
                        lon[1] = mid;
                    }
                }
                else
                {
                    mid = (lat[0] + lat[1]) / 2;
                    if (latitude > mid)
                    {
                        ch |= (1 << (4 - bit));
                        lat[0] = mid;
                    }
                    else
                    {
                        lat[1] = mid;
                    }
                }

                isEven = !isEven;
                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    geohash[geohash.Length - precision] = base32[ch];
                    precision--;
                    bit = 0;
                    ch = 0;
                }
            }

            return new string(geohash);
        }
    }
}

