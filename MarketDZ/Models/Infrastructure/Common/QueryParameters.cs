using System.Text;
using MarketDZ.Models.Core.Enums;
using MarketDZ.Models.Filters;


namespace MarketDZ.Models.Infrastructure.Common
{
    /// <summary>
    /// Base implementation of IQueryParameters
    /// </summary>
    public class QueryParameters : IQueryParameters
    {
        private readonly IList<SortCriteria> sortCriteria = new List<SortCriteria>();

        /// <summary>
        /// Number of items to skip (for pagination)
        /// </summary>
        public int Skip { get; set; } = 0;

        /// <summary>
        /// Maximum number of items to take (for pagination)
        /// </summary>
        public int Take { get; set; } = 50;

        /// <summary>
        /// Collection of sorting criteria
        /// </summary>
        public IList<SortCriteria> SortCriteria => sortCriteria;
        /// <summary>
        /// Collection of filter criteria
        /// </summary>
        public IList<FilterCriteria> FilterCriteria { get; } = new List<FilterCriteria>();

        /// <summary>
        /// Adds a filter condition
        /// </summary>
        /// <param name="field">Name of the field to filter on</param>
        /// <param name="op">Filter operation</param>
        /// <param name="value">Value to compare against</param>
        public void AddFilter(string field, FilterOperator op, object value)
        {
            if (string.IsNullOrEmpty(field))
                throw new ArgumentException("Field cannot be null or empty", nameof(field));

            FilterCriteria.Add(new FilterCriteria(field, op, value));
        }

        /// <summary>
        /// Adds a sorting condition
        /// </summary>
        /// <param name="field">Name of the field to sort on</param>
        /// <param name="direction">Sort direction</param>
        public void AddSort(string field, SortDirection direction)
        {
            if (string.IsNullOrEmpty(field))
                throw new ArgumentException("Field cannot be null or empty", nameof(field));

            SortCriteria.Add(new SortCriteria(field, direction));
        }

        /// <summary>
        /// Creates a cache key representing these query parameters
        /// </summary>
        /// <returns>String representation for caching</returns>
        public string GetCacheKey()
        {
            ValidateParameters();

            var keyBuilder = new StringBuilder();

            // Add pagination info
            keyBuilder.Append($"skip={Skip}:take={Take}");

            // Add sorts (ensure deterministic order)
            if (SortCriteria.Any())
            {
                keyBuilder.Append(":sort=");
                var sortedSorts = SortCriteria
                    .OrderBy(s => s.Field)
                    .ThenBy(s => s.Direction)
                    .Select(s => $"{SanitizeValue(s.Field)}_{s.Direction}");
                keyBuilder.Append(string.Join(",", sortedSorts));
            }

            // Add filters (ensure deterministic order)
            if (FilterCriteria.Any())
            {
                keyBuilder.Append(":filter=");
                var sortedFilters = FilterCriteria
                    .OrderBy(f => f.Field)
                    .ThenBy(f => f.Operator)
                    .ThenBy(f => SanitizeValue(f.Value?.ToString() ?? "null"))
                    .Select(f =>
                    {
                        var valueStr = f.Value == null ? "null" : JsonConvert.SerializeObject(f.Value);
                        return $"{SanitizeValue(f.Field)}_{f.Operator}_{SanitizeValue(valueStr)}";
                    });
                keyBuilder.Append(string.Join(",", sortedFilters));
            }

            return keyBuilder.ToString();
        }
        /// <summary>
        /// Validates query parameters for consistency and correctness
        /// </summary>
        private void ValidateParameters()
        {
            if (Skip < 0)
                throw new ArgumentException("Skip cannot be negative", nameof(Skip));

            if (Take <= 0)
                throw new ArgumentException("Take must be greater than zero", nameof(Take));

            foreach (var filter in FilterCriteria)
            {
                if (string.IsNullOrEmpty(filter.Field))
                    throw new ArgumentException("Filter field cannot be null or empty");

                if (filter.Value == null)
                    throw new ArgumentException($"Filter value for field '{filter.Field}' cannot be null");
            }

            foreach (var sort in SortCriteria)
            {
                if (string.IsNullOrEmpty(sort.Field))
                    throw new ArgumentException("Sort field cannot be null or empty");
            }
        }

        /// <summary>
        /// Sanitizes a value to ensure it is safe for inclusion in cache keys
        /// </summary>
        /// <param name="value">The value to sanitize</param>
        /// <returns>Sanitized string</returns>
        private string SanitizeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "null";

            // Replace characters that could break cache key formatting
            return value.Replace(":", "_").Replace(",", "_").Replace("=", "_");
        }

    }
}