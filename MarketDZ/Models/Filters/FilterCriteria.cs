using System;
using System.Collections.Generic;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Filters
{
    /// <summary>
    /// Comprehensive filter criteria for API requests and database queries
    /// </summary>
    public class FilterCriteria
    {
        // Constructor for simple field-operator-value filtering
        public FilterCriteria(string field, FilterOperator op, object value)
        {
            Field = field;
            Operator = op;
            Value = value;

            // Initialize collections and default values
            Categories = new List<string>();
            Tags = new List<string>();
            SearchText = string.Empty; // Initialize to avoid nullability issues
        }

        // Default constructor for property-based filtering
        public FilterCriteria()
        {
            Field = string.Empty; // Initialize to avoid nullability issues
            Value = new object(); // Initialize to avoid nullability issues
            Categories = new List<string>();
            Tags = new List<string>();
            SearchText = string.Empty; // Initialize to avoid nullability issues
        }

        // Simple filter properties needed by FirebaseQueryConverter
        public string Field { get; set; }
        public FilterOperator Operator { get; set; }
        public object Value { get; set; }

        // Basic filter criteria (existing properties)
        public string SearchText { get; set; }
        public List<string> Categories { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public AlState? State { get; set; }
        public ItemStatus? Status { get; set; }

        // Location-based criteria
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? RadiusKm { get; set; }
        public bool SortByDistance { get; set; }

        // Date range criteria
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // Sorting options
        public SortOption? SortBy { get; set; }

        // Pagination
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        // Advanced filtering options
        public ForSaleCategory? ForSaleCategory { get; set; }
        public ForRentCategory? ForRentCategory { get; set; }
        public JobCategory? JobCategory { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }

        // Tags/keywords for more advanced searching
        public List<string> Tags { get; set; }

        /// <summary>
        /// Converts to FilterParameters for internal use
        /// </summary>
        public FilterParameters ToFilterParameters()
        {
            var parameters = new FilterParameters
            {
                SearchText = this.SearchText,
                MinPrice = this.MinPrice,
                MaxPrice = this.MaxPrice,
                State = this.State,
                Status = this.Status ?? ItemStatus.Active,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                RadiusKm = this.RadiusKm,
                SortByDistance = this.SortByDistance,
                FromDate = this.DateFrom,
                ToDate = this.DateTo,
                SortBy = this.SortBy,
                ForSaleCategory = this.ForSaleCategory,
                ForRentCategory = this.ForRentCategory,
                JobCategory = this.JobCategory,
                ServiceCategory = this.ServiceCategory,
                Skip = (Page - 1) * PageSize,
                Take = PageSize,
                Tags = new List<string>(this.Tags)
            };

            // Handle categories
            if (Categories?.Count > 0)
            {
                parameters.Category = Categories[0];
            }

            parameters.DeterminePrimaryFilter();
            return parameters;
        }

        /// <summary>
        /// Creates a deep clone of the filter criteria
        /// </summary>
        public FilterCriteria Clone()
        {
            return new FilterCriteria
            {
                Field = this.Field,
                Operator = this.Operator,
                Value = this.Value,
                SearchText = this.SearchText,
                Categories = new List<string>(this.Categories),
                MinPrice = this.MinPrice,
                MaxPrice = this.MaxPrice,
                State = this.State,
                Status = this.Status,
                Latitude = this.Latitude,
                Longitude = this.Longitude,
                RadiusKm = this.RadiusKm,
                SortByDistance = this.SortByDistance,
                DateFrom = this.DateFrom,
                DateTo = this.DateTo,
                SortBy = this.SortBy,
                Page = this.Page,
                PageSize = this.PageSize,
                ForSaleCategory = this.ForSaleCategory,
                ForRentCategory = this.ForRentCategory,
                JobCategory = this.JobCategory,
                ServiceCategory = this.ServiceCategory,
                Tags = new List<string>(this.Tags)
            };
        }
    }
}