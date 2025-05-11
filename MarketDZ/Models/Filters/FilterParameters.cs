using System;
using System.Collections.Generic;
using System.Linq;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Dtos.Item;

namespace MarketDZ.Models.Filters
{
    /// <summary>
    /// Comprehensive filter parameters for querying items
    /// </summary>
    public class FilterParameters 
    {
        internal object? PageSize;

        // Basic filters
        public string? SearchText { get; set; }
        public string? Category { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public AlState? State { get; set; }
        public ItemStatus? Status { get; set; } = ItemStatus.Active; // Default to active items

        // Category-specific filters
        public ForSaleCategory? ForSaleCategory { get; set; }
        public ForRentCategory? ForRentCategory { get; set; }
        public JobCategory? JobCategory { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }

        public string? PrimaryFilterField { get; set; }
        public object? PrimaryFilterValue { get; set; }
        public string? OrderByField { get; set; }

        // Location-based filters
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public bool SortByDistance { get; set; }

        // Date range filters
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        // Sorting options
        public SortOption? SortBy { get; set; } = SortOption.DateNewest; // Default to newest first

        // Pagination
        public int Skip { get; set; } = 0;  // Instead of Skip
        public int Take { get; set; } = 20; // Instead of Take
        // User-specific filters
        public string?  UserId { get; set; }

        public bool FavoritesOnly { get; set; }

        // Tags/Keywords filter (for more advanced searching)
        public List<string> Tags { get; set; } = new List<string>();

        // Primary filter type (used to optimize query performance)
        private FilterType PrimaryFilterType { get; set; } = FilterType.None;
        public string? SearchTerm { get; internal set; }
        public int Page { get; internal set; }



        /// <summary>
        /// Creates a FilterParameters instance from FilterCriteria
        /// </summary>
        public static FilterParameters FromFilterCriteria(FilterCriteria criteria)
        {
            if (criteria == null) return new FilterParameters();

            var filter = new FilterParameters
            {
                SearchText = criteria.SearchText,
                MinPrice = criteria.MinPrice,
                MaxPrice = criteria.MaxPrice,
                State = criteria.State,
                Latitude = criteria.Latitude,
                Longitude = criteria.Longitude,
                RadiusKm = criteria.RadiusKm,
                FromDate = criteria.DateFrom,
                ToDate = criteria.DateTo,
                SortBy = criteria.SortBy,
                SortByDistance = criteria.SortByDistance,
                Status = ItemStatus.Active // Default to active items
            };

            // Handle categories (convert from list to single value if needed)
            if (criteria.Categories?.Any() == true)
            {
                filter.Category = criteria.Categories.First();
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        /// <summary>
        /// Creates a FilterParameters instance from ProximitySearchRequest
        /// </summary>
        public static FilterParameters FromProximitySearch(ProximitySearchRequest request)
        {
            if (request == null) return new FilterParameters();

            var filter = new FilterParameters
            {
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                SortByDistance = true,
                Status = ItemStatus.Active
            };

            filter.DeterminePrimaryFilter();
            return filter;
        }

        /// <summary>
        /// Determines the most efficient primary filter for query optimization
        /// </summary>
        public void DeterminePrimaryFilter()
        {
            // Logic to determine which filter will be most efficient for the database query
            // We prioritize based on which filter will reduce the result set the most

            if (!string.IsNullOrEmpty(Category))
            {
                PrimaryFilterType = FilterType.Category;
            }
            else if (State.HasValue)
            {
                PrimaryFilterType = FilterType.State;
            }
            else if (!string.IsNullOrEmpty(UserId))
            {
                PrimaryFilterType = FilterType.User;
            }
            else if (Latitude.HasValue && Longitude.HasValue && RadiusKm.HasValue)
            {
                PrimaryFilterType = FilterType.Location;
            }
            else if (Status.HasValue)
            {
                PrimaryFilterType = FilterType.Status;
            }
            else if (MinPrice.HasValue || MaxPrice.HasValue)
            {
                PrimaryFilterType = FilterType.Price;
            }
            else if (FromDate.HasValue || ToDate.HasValue)
            {
                PrimaryFilterType = FilterType.Date;
            }
            else if (!string.IsNullOrEmpty(SearchText))
            {
                PrimaryFilterType = FilterType.Text;
            }
            else
            {
                PrimaryFilterType = FilterType.None;
            }
        }

        /// <summary>
        /// Gets the primary filter type for query optimization
        /// </summary>
        public FilterType GetPrimaryFilterType()
        {
            return PrimaryFilterType;
        }

        /// <summary>
        /// Creates a copy of the filter parameters with pagination adjusted for the next page
        /// </summary>
        public FilterParameters GetNextPage()
        {
            var nextPage = Clone();
            nextPage.Skip += nextPage.Take;
            return nextPage;
        }

        /// <summary>
        /// Creates a copy of the filter parameters with pagination adjusted for the previous page
        /// </summary>
        public FilterParameters GetPreviousPage()
        {
            var previousPage = Clone();
            previousPage.Skip = Math.Max(0, previousPage.Skip - previousPage.Take);
            return previousPage;
        }

        /// <summary>
        /// Creates a deep clone of the filter parameters
        /// </summary>
        public FilterParameters Clone()
        {
            return new FilterParameters
            {
                SearchText = this.SearchText,
                Category = this.Category,
                MinPrice = this.MinPrice,
                MaxPrice = this.MaxPrice,
                State = this.State,
                Status = this.Status,
                ForSaleCategory = this.ForSaleCategory,
                ForRentCategory = this.ForRentCategory,
                JobCategory = this.JobCategory,
                ServiceCategory = this.ServiceCategory,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                RadiusKm = this.RadiusKm,
                SortByDistance = this.SortByDistance,
                FromDate = this.FromDate,
                ToDate = this.ToDate,
                SortBy = this.SortBy,
                Skip = this.Skip,
                Take = this.Take,
                UserId = this.UserId,
                FavoritesOnly = this.FavoritesOnly,
                PrimaryFilterField = this.PrimaryFilterField,
                PrimaryFilterValue = this.PrimaryFilterValue,
                OrderByField = this.OrderByField,
                Tags = new List<string>(this.Tags),
                PrimaryFilterType = this.PrimaryFilterType
            };
        }

        /// <summary>
        /// Determines if the query should use descending order
        /// </summary>
        /// <returns>True if results should be sorted in descending order</returns>
        public bool OrderDescending()
        {
            // Logic to determine if ordering should be descending
            // Typical logic based on SortBy property:
            return SortBy == SortOption.DateNewest ||
                   SortBy == SortOption.PriceHighToLow;
        }
        public static FilterParameters GetActiveItems(string category = null)
        {
            var filter = new FilterParameters
            {
                Status = ItemStatus.Active,
                SortBy = SortOption.DateNewest
            };

            if (!string.IsNullOrEmpty(category))
            {
                filter.Category = category;
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        public static FilterParameters GetUserItems(string?  userId, ItemStatus? status = null)
        {
            var filter = new FilterParameters
            {
                UserId = userId,
                SortBy = SortOption.DateNewest
            };

            if (status.HasValue)
            {
                filter.Status = status.Value;
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        public static FilterParameters GetNearbyItems(double latitude, double longitude, double radiusKm = 10, string category = null)
        {
            var filter = new FilterParameters
            {
                Latitude = latitude,
                Longitude = longitude,
                RadiusKm = radiusKm,
                Status = ItemStatus.Active,
                SortByDistance = true
            };

            if (!string.IsNullOrEmpty(category))
            {
                filter.Category = category;
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        public static FilterParameters GetSearchItems(string searchText, string category = null)
        {
            var filter = new FilterParameters
            {
                SearchText = searchText,
                Status = ItemStatus.Active,
                SortBy = SortOption.Relevance
            };

            if (!string.IsNullOrEmpty(category))
            {
                filter.Category = category;
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        /// <summary>
        /// Filter types for query optimization
        /// </summary>
        public enum FilterType
        {
            None,
            Text,
            Category,
            State,
            Price,
            Location,
            Date,
            User,
            Status
        }
    }
}