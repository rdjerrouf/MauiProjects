using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Core.ValueObjects
{
        /// <summary>
        /// Represents a geo-indexed item
        /// </summary>
        public class GeoIndexedItem
        {
            public string? Id { get; set; }
            public string? ItemId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string? Geohash { get; set; }
            public DateTime IndexedAt { get; set; }
        }

        /// <summary>
        /// Result of a migration operation
        /// </summary>
        public class MigrationResult
        {
            public bool Success { get; set; }
            public int ItemsMigrated { get; set; }
            public int ItemsFailed { get; set; }
            public List<string>? FailedItemIds { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime CompletedAt { get; set; }
            public TimeSpan Duration => CompletedAt - StartedAt;

        }
    }

