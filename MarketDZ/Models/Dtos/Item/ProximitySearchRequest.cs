using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Dtos.Item
{
    /// <summary>
    /// Request model for proximity-based searches
    /// </summary>
    public class ProximitySearchRequest
    {
        /// <summary>
        /// Latitude of the center point for the search
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude of the center point for the search
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Maximum distance in kilometers from the center point
        /// </summary>
        public double RadiusKm { get; set; } = 10; // Default to 10km

        /// <summary>
        /// Optional category ID to filter by
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// Optional search term to filter by
        /// </summary>
        public string? SearchTerm { get; set; }

        /// <summary>
        /// Optional minimum price
        /// </summary>
        public decimal? MinPrice { get; set; }

        /// <summary>
        /// Optional maximum price
        /// </summary>
        public decimal? MaxPrice { get; set; }

        /// <summary>
        /// Flag to sort by distance (true) or most recent (false)
        /// </summary>
        public bool SortByDistance { get; set; } = true;
        public ICollection<FileResult> Photos { get; set; } = new List<FileResult>();
    }
}