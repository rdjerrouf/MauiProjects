using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Firebase.Database;
using Firebase.Database.Query;
using MarketDZ.Models.Filters;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Data
{
    /// <summary>
    /// Converts database-agnostic query parameters to Firebase-specific queries
    /// </summary>
    public class FirebaseQueryConverter
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Creates a new Firebase query converter
        /// </summary>
        /// <param name="logger">Logger</param>
        public FirebaseQueryConverter(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Applies client-side filtering and sorting to Firebase query results
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="entities">Entities from Firebase</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Filtered and sorted entities</returns>
        public IEnumerable<T> ApplyClientSideFilters<T>(IEnumerable<T> entities, IQueryParameters parameters)
        {
            if (entities == null)
                return Enumerable.Empty<T>();

            if (parameters == null)
                return entities;

            var result = entities;

            // Apply all filters client-side
            foreach (var filter in parameters.FilterCriteria)
            {
                result = ApplyFilter(result, filter);
            }

            // Apply all sorts client-side
            foreach (var sort in parameters.SortCriteria)
            {
                result = ApplySort(result, sort);
            }

            // Apply pagination
            if (parameters.Skip > 0)
            {
                result = result.Skip(parameters.Skip);
            }

            if (parameters.Take > 0)
            {
                result = result.Take(parameters.Take);
            }

            return result;
        }

        /// <summary>
        /// Builds a Firebase query from database-agnostic query parameters
        /// </summary>
        /// <param name="firebaseClient">Firebase client instance</param>
        /// <param name="path">Database path</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Firebase child query</returns>
        public ChildQuery BuildQuery(
              FirebaseClient firebaseClient,
              string path,
              IQueryParameters parameters)
        {
            if (firebaseClient == null)
                throw new ArgumentNullException(nameof(firebaseClient));

            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            // Start with the base path
            ChildQuery query = firebaseClient.Child(path);

            // If no parameters, return basic query
            if (parameters == null ||
                (!parameters.FilterCriteria.Any() &&
                 !parameters.SortCriteria.Any() &&
                 parameters.Skip <= 0 &&
                 parameters.Take <= 0))
            {
                return query;
            }

            try
            {
                // Log that server-side filtering may be limited
                _logger.LogInformation("Building Firebase query for path {Path}", path);
                _logger.LogInformation("Note: Firebase's query capabilities are limited; filtering will be applied client-side");

                // Since we can't transform the query directly due to API constraints in this SDK,
                // we'll return the base query and rely on client-side filtering
                return query;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building Firebase query for path {Path}", path);
                return query;
            }
        }

        /// <summary>
        /// Converts a filter value to a format Firebase can use
        /// </summary>
        private string ConvertFilterValue(object value)
        {
            if (value == null)
                return null;

            // Firebase RTDB has specific handling for different types
            if (value is DateTime dateTime)
            {
                // Convert to ISO string format which works well with Firebase
                return dateTime.ToString("o");
            }
            else
            {
                // For other types, convert to string
                return value.ToString();
            }
        }

        /// <summary>
        /// Applies a filter criterion client-side
        /// </summary>
        private IEnumerable<T> ApplyFilter<T>(IEnumerable<T> entities, FilterCriteria filter)
        {
            // Use reflection to get the property value
            var propertyInfo = typeof(T).GetProperty(filter.Field);
            if (propertyInfo == null)
            {
                _logger.LogWarning("Property {Property} not found on type {Type}", filter.Field, typeof(T).Name);
                return entities;
            }

            // Apply filter based on operator
            switch (filter.Operator)
            {
                case FilterOperator.Equal:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) == 0);

                case FilterOperator.NotEqual:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) != 0);

                case FilterOperator.GreaterThan:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) > 0);

                case FilterOperator.GreaterThanOrEqual:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) >= 0);

                case FilterOperator.LessThan:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) < 0);

                case FilterOperator.LessThanOrEqual:
                    return entities.Where(e =>
                        CompareValues(propertyInfo.GetValue(e), filter.Value) <= 0);

                case FilterOperator.Contains:
                    return entities.Where(e =>
                        propertyInfo.GetValue(e)?.ToString()?.Contains(filter.Value?.ToString() ?? "") == true);

                case FilterOperator.StartsWith:
                    return entities.Where(e =>
                        propertyInfo.GetValue(e)?.ToString()?.StartsWith(filter.Value?.ToString() ?? "") == true);

                case FilterOperator.EndsWith:
                    return entities.Where(e =>
                        propertyInfo.GetValue(e)?.ToString()?.EndsWith(filter.Value?.ToString() ?? "") == true);

                case FilterOperator.Exists:
                    return entities.Where(e => propertyInfo.GetValue(e) != null);

                default:
                    _logger.LogWarning("Filter operator {Operator} not implemented for client-side filtering", filter.Operator);
                    return entities;
            }
        }

        /// <summary>
        /// Applies a sort criterion client-side
        /// </summary>
        private IEnumerable<T> ApplySort<T>(IEnumerable<T> entities, SortCriteria sort)
        {
            // Use reflection to get the property
            var propertyInfo = typeof(T).GetProperty(sort.Field);
            if (propertyInfo == null)
            {
                _logger.LogWarning("Property {Property} not found on type {Type}", sort.Field, typeof(T).Name);
                return entities;
            }

            // Apply sort based on direction
            switch (sort.Direction)
            {
                case SortDirection.Ascending:
                    return entities.OrderBy(e => propertyInfo.GetValue(e));

                case SortDirection.Descending:
                    return entities.OrderByDescending(e => propertyInfo.GetValue(e));

                default:
                    _logger.LogWarning("Unknown sort direction {Direction}, defaulting to ascending", sort.Direction);
                    return entities.OrderBy(e => propertyInfo.GetValue(e));
            }
        }

        /// <summary>
        /// Compares two values of potentially different types
        /// </summary>
        private int CompareValues(object value1, object value2)
        {
            if (value1 == null && value2 == null)
                return 0;

            if (value1 == null)
                return -1;

            if (value2 == null)
                return 1;

            // Try to convert to comparable types
            if (value1 is IComparable comparable1 && value1.GetType() == value2.GetType())
            {
                return comparable1.CompareTo(value2);
            }

            // Try string conversion as fallback
            return string.Compare(value1.ToString(), value2.ToString(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Represents sorting criteria for queries.
    /// </summary>
    public class SortCriteria
    {
        public SortCriteria(string field, SortDirection direction)
        {
            Field = field;
            Direction = direction;
        }

        /// <summary>
        /// The field to sort by.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The direction of the sort (Ascending or Descending).
        /// </summary>
        public SortDirection Direction { get; set; }
    }
}