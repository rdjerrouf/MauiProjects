using MarketDZ.Services.DbServices;
using MarketDZ.Models.Filters;
using MarketDZ.Services.DbServices.Firebase;



namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Defines parameters for querying data stores in a database-agnostic way
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
        /// Adds a filter condition
        /// </summary>
        /// <param name="field">Name of the field to filter on</param>
        /// <param name="op">Filter operation</param>
        /// <param name="value">Value to compare against</param>
        void AddFilter(string field, FilterOperator op, object value);

        /// <summary>
        /// Adds a sorting condition
        /// </summary>
        /// <param name="field">Name of the field to sort on</param>
        /// <param name="direction">Sort direction</param>
        void AddSort(string field, SortDirection direction);

        /// <summary>
        /// Creates a cache key representing these query parameters
        /// </summary>
        /// <returns>String representation for caching</returns>
        string GetCacheKey();

    }
}
