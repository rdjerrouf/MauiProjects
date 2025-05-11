// PaginatedResult.cs
using System;
using System.Collections.Generic;

namespace MarketDZ.Models.Infrastructure.Common
{
    /// <summary>
    /// Represents a paginated result set
    /// </summary>
    public class PaginatedResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public int TotalPages { get; set; }

        // Using computed properties for consistency
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }

        /// <summary>
        /// Creates a new paginated result with default values
        /// </summary>
        public PaginatedResult()
        {
        }

        /// <summary>
        /// Creates a new paginated result with the specified items and pagination information
        /// </summary>
        public PaginatedResult(List<T> items, int totalItems, int page, int pageSize)
        {
            Items = items ?? new List<T>();
            TotalItems = totalItems;
            Page = Math.Max(1, page);
            PageSize = Math.Max(1, pageSize);
            TotalPages = (int)Math.Ceiling((double)TotalItems / PageSize);
        }

        /// <summary>
        /// Gets the information needed to build the next page request
        /// </summary>
        public (int page, int pageSize) GetNextPageInfo()
        {
            return HasNextPage ? (Page + 1, PageSize) : (Page, PageSize);
        }

        /// <summary>
        /// Gets the information needed to build the previous page request
        /// </summary>
        public (int page, int pageSize) GetPreviousPageInfo()
        {
            return HasPreviousPage ? (Page - 1, PageSize) : (Page, PageSize);
        }
    }
}