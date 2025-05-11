using System;
using System.Collections.Generic;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Enums;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Services.Core.Models;

namespace MarketDZ.Models.Filters
{
    /// <summary>
    /// Database-agnostic query parameters for items, adapted from FilterParameters
    /// </summary>
    public class ItemQueryParameters : QueryParameters
    {
        /// <summary>
        /// Creates a new instance of ItemQueryParameters
        /// </summary>
        public ItemQueryParameters()
        {
        }

        /// <summary>
        /// Creates query parameters from FilterParameters
        /// </summary>
        /// <param name="filter">Filter parameters</param>
        public static ItemQueryParameters FromFilterParameters(FilterParameters filter)
        {
            if (filter == null)
                return new ItemQueryParameters();

            var parameters = new ItemQueryParameters
            {
                Skip = filter.Skip,
                Take = filter.Take > 0 ? filter.Take : 50
            };

            // Add filters based on FilterParameters properties
            if (!string.IsNullOrEmpty(filter.SearchText))
            {
                // For text search, we need client-side filtering
                // Most databases don't support contains operations natively
                parameters.AddFilter("Title", FilterOperator.Contains, filter.SearchText);
                // Also search in description
                parameters.AddFilter("Description", FilterOperator.Contains, filter.SearchText);
            }

            if (!string.IsNullOrEmpty(filter.Category))
            {
                parameters.AddFilter("Category", FilterOperator.Equal, filter.Category);
            }

            if (filter.Status.HasValue)
            {
                parameters.AddFilter("Status", FilterOperator.Equal, filter.Status.Value);
            }

            if (filter.MinPrice.HasValue)
            {
                parameters.AddFilter("Price", FilterOperator.GreaterThanOrEqual, filter.MinPrice.Value);
            }

            if (filter.MaxPrice.HasValue)
            {
                parameters.AddFilter("Price", FilterOperator.LessThanOrEqual, filter.MaxPrice.Value);
            }

            if (filter.State.HasValue)
            {
                parameters.AddFilter("State", FilterOperator.Equal, filter.State.Value);
            }

            if (filter.FromDate.HasValue)
            {
                parameters.AddFilter("ListedDate", FilterOperator.GreaterThanOrEqual, filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                parameters.AddFilter("ListedDate", FilterOperator.LessThanOrEqual, filter.ToDate.Value);
            }

            if (!string.IsNullOrEmpty(filter.UserId))
            {
                parameters.AddFilter("PostedByUserId", FilterOperator.Equal, filter.UserId);
            }

            // Add category-specific filters
            if (filter.ForSaleCategory.HasValue)
            {
                parameters.AddFilter("ForSaleCategory", FilterOperator.Equal, filter.ForSaleCategory.Value);
            }

            if (filter.ForRentCategory.HasValue)
            {
                parameters.AddFilter("ForRentCategory", FilterOperator.Equal, filter.ForRentCategory.Value);
            }

            if (filter.JobCategory.HasValue)
            {
                parameters.AddFilter("JobCategory", FilterOperator.Equal, filter.JobCategory.Value);
            }

            if (filter.ServiceCategory.HasValue)
            {
                parameters.AddFilter("ServiceCategory", FilterOperator.Equal, filter.ServiceCategory.Value);
            }

            // Add tags filtering if available
            if (filter.Tags != null && filter.Tags.Count > 0)
            {
                // Tags are typically stored as a comma-separated list or in a separate collection
                // This is a simplified approach that may need adjustment based on your data model
                foreach (var tag in filter.Tags)
                {
                    parameters.AddFilter("Tags", FilterOperator.Contains, tag);
                }
            }

            // Add sorting based on SortOption
            switch (filter.SortBy)
            {
                case SortOption.PriceLowToHigh:
                    parameters.AddSort("Price", SortDirection.Ascending);
                    break;

                case SortOption.PriceHighToLow:
                    parameters.AddSort("Price", SortDirection.Descending);
                    break;

                case SortOption.DateNewest:
                    parameters.AddSort("ListedDate", SortDirection.Descending);
                    break;

                case SortOption.DateOldest:
                    parameters.AddSort("ListedDate", SortDirection.Ascending);
                    break;

                default:
                    // Default to newest first
                    parameters.AddSort("ListedDate", SortDirection.Descending);
                    break;
            }

            return parameters;
        }

        /// <summary>
        /// Creates query parameters from FilterCriteria
        /// </summary>
        /// <param name="criteria">Filter criteria</param>
        public static ItemQueryParameters FromFilterCriteria(FilterCriteria criteria)
        {
            if (criteria == null)
                return new ItemQueryParameters();

            // Convert filter criteria to filter parameters first
            var filterParams = criteria.ToFilterParameters();

            // Then convert filter parameters to query parameters
            return FromFilterParameters(filterParams);
        }
    }
}