using MarketDZ.Models.Filters;
using System.Collections.Generic;
using MarketDZ.Services.Core.Models;
using MarketDZ.Models.Infrastructure.Common;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Interface for database-agnostic query parameters
    /// </summary>
    public interface IQueryParameters
    {
        /// <summary>
        /// Number of items to skip (for pagination)
        /// </summary>
        int Skip { get; set; }

        /// <summary>
        /// Maximum number of items to take (for pagination)
        /// </summary>
        int Take { get; set; }

        /// <summary>
        /// Collection of sorting criteria
        /// </summary>
        IList<SortCriteria> SortCriteria { get; }

        /// <summary>
        /// Collection of filter criteria
        /// </summary>
        IList<FilterCriteria> FilterCriteria { get; }

        /// <summary>
        /// Custom parameters for database-specific features
        /// </summary>
        IDictionary<string, object> CustomParameters { get; }

        /// <summary>
        /// Adds a filter condition
        /// </summary>
        /// <param name="field">Field name to filter on</param>
        /// <param name="op">Filter operator</param>
        /// <param name="value">Value to compare against</param>
        /// <returns>This instance for method chaining</returns>
        IQueryParameters AddFilter(string field, FilterOperator op, object value);

        /// <summary>
        /// Adds a sorting condition
        /// </summary>
        /// <param name="field">Field name to sort on</param>
        /// <param name="direction">Sort direction</param>
        /// <returns>This instance for method chaining</returns>
        IQueryParameters AddSort(string field, SortDirection direction);

        /// <summary>
        /// Adds a custom parameter
        /// </summary>
        /// <param name="key">Parameter key</param>
        /// <param name="value">Parameter value</param>
        /// <returns>This instance for method chaining</returns>
        IQueryParameters AddCustomParameter(string key, object value);

        /// <summary>
        /// Creates a cache key for these parameters
        /// </summary>
        /// <returns>String representation for caching</returns>
        string GetCacheKey();
    }
}