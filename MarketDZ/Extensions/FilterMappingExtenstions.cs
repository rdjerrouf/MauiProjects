using System;
using System.Collections.Generic;
using System.Linq;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Dtos.Item;
using MarketDZ.Models.Filters;
using MarketDZ.Models.Infrastructure.Common;
using MarketDZ.Services.Core.Models;
//using MarketDZ.ViewModels;

namespace MarketDZ.Extensions
{
    /// <summary>
    /// Extension methods for mapping between filter-related classes
    /// </summary>
    public static class FilterMappingExtensions
    {
        /// <summary>
        /// Converts FilterCriteria to FilterParameters
        /// </summary>
        public static FilterParameters ToFilterParameters(this FilterCriteria criteria)
        {
            return FilterParameters.FromFilterCriteria(criteria);
        }

        /// <summary>
        /// Converts ProximitySearchRequest to FilterParameters
        /// </summary>
        public static FilterParameters ToFilterParameters(this ProximitySearchRequest request)
        {
            return FilterParameters.FromProximitySearch(request);
        }

        /// <summary>
        /// Maps Item to ItemListDto for efficient list displays
        /// </summary>
        public static ItemListDto? ToListDto(this Item item)
        {
            if (item == null) return null;

            return new ItemListDto
            {
                Id = item.Id,
                Title = item.Title,
                Price = item.Price,
                Category = item.Category.ToString(), // Convert enum to string
                PrimaryPhotoUrl = item.PhotoUrl ?? item.ImageUrl,
                State = item.State,
                Status = item.Status,
                ListedDate = item.ListedDate,
                Latitude = item.Latitude,
                Longitude = item.Longitude
            };
        }
        /// <summary>
        /// Maps a collection of Items to a collection of ItemListDtos
        /// </summary>
        public static IEnumerable<ItemListDto> ToListDtos(this IEnumerable<Item> items)
        {
            return items?.Select(item => item.ToListDto()) ?? new List<ItemListDto>();
        }

        /// <summary>
        /// Converts SearchViewModel state to FilterParameters
        /// </summary>
        public static FilterParameters ToFilterParameters(this SearchViewModel viewModel)
        {
            if (viewModel == null) return new FilterParameters();

            var filter = new FilterParameters
            {
                SearchText = viewModel.SearchQuery,
                Category = viewModel.SelectedCategory,
                MinPrice = (decimal?)viewModel.MinPrice,
                MaxPrice = viewModel.MaxPrice < decimal.MaxValue ? (decimal?)viewModel.MaxPrice : null,
                State = viewModel.SelectedState,
                Status = ItemStatus.Active // Default to active items
            };

            // Add subcategory filtering
            if (viewModel.SelectedSubCategory != null)
            {
                switch (viewModel.SelectedCategory)
                {
                    case "For Sale":
                        filter.ForSaleCategory = (ForSaleCategory)viewModel.SelectedSubCategory;
                        break;
                    case "Rentals":
                    case "For Rent":
                        filter.ForRentCategory = (ForRentCategory)viewModel.SelectedSubCategory;
                        break;
                    case "Jobs":
                        filter.JobCategory = (JobCategory)viewModel.SelectedSubCategory;
                        break;
                    case "Services":
                        filter.ServiceCategory = (ServiceCategory)viewModel.SelectedSubCategory;
                        break;
                }
            }

            // Add pagination if the view model supports it
            if (viewModel.Page > 0 && viewModel.PageSize > 0)
            {
                filter.Skip = (viewModel.Page - 1) * viewModel.PageSize;
                filter.Take = viewModel.PageSize;
            }

            filter.DeterminePrimaryFilter();
            return filter;
        }

        /// <summary>
        /// Creates a proximity filter from the NearbyItemsViewModel
        /// </summary>
        public static FilterParameters ToFilterParameters(this NearbyItemsViewModel viewModel)
        {
            if (viewModel == null || viewModel.CurrentLocation == null)
                return new FilterParameters();

            var filter = new FilterParameters
            {
                Latitude = viewModel.CurrentLocation.Latitude,
                Longitude = viewModel.CurrentLocation.Longitude,
                RadiusKm = viewModel.SearchRadius,
                Status = ItemStatus.Active,
                SortByDistance = true
            };

            filter.DeterminePrimaryFilter();
            return filter;
        }

        /// <summary>
        /// Converts a PaginatedResult of Items to a PaginatedViewModel of ItemListDtos
        /// </summary>
        public static PaginatedViewModel<ItemListDto> ToListDtoPaginatedViewModel(
            this PaginatedResult<Item> result,
            FilterParameters filter)
        {
            return new PaginatedViewModel<ItemListDto>
            {
                Items = result.Items.Select(item => item.ToListDto()).ToList(),
                TotalItems = result.TotalItems,
                CurrentPage = result.Page,
                PageSize = result.PageSize,
                TotalPages = result.TotalPages,
                HasNextPage = result.HasNextPage,
                HasPreviousPage = result.HasPreviousPage,
                AppliedFilters = filter
            };
        }

        /// <summary>
        /// Converts a PaginatedResult to a PaginatedViewModel
        /// </summary>
        public static PaginatedViewModel<T> ToPaginatedViewModel<T>(
            this PaginatedResult<T> result,
            FilterParameters filter)
        {
            return new PaginatedViewModel<T>
            {
                Items = result.Items,
                TotalItems = result.TotalItems,
                CurrentPage = result.Page,
                PageSize = result.PageSize,
                TotalPages = result.TotalPages,
                HasNextPage = result.HasNextPage,
                HasPreviousPage = result.HasPreviousPage,
                AppliedFilters = filter
            };
        }

        /// <summary>
        /// Converts FilterParameters to FilterCriteria
        /// </summary>
        public static FilterCriteria ToFilterCriteria(this FilterParameters parameters)
        {
            if (parameters == null) return new FilterCriteria(); // Return a new instance instead of null

            return new FilterCriteria
            {
                SearchText = parameters.SearchText ?? string.Empty, // Fix for CS8601: Ensure non-null value
                Categories = string.IsNullOrEmpty(parameters.Category)
                    ? new List<string>()
                    : new List<string> { parameters.Category },
                MinPrice = parameters.MinPrice,
                MaxPrice = parameters.MaxPrice,
                State = parameters.State,
                Status = parameters.Status,
                Latitude = parameters.Latitude,
                Longitude = parameters.Longitude,
                RadiusKm = parameters.RadiusKm,
                DateFrom = parameters.FromDate,
                DateTo = parameters.ToDate,
                SortBy = parameters.SortBy,
                SortByDistance = parameters.SortByDistance,
                Page = parameters.Skip / Math.Max(1, parameters.Take) + 1,
                PageSize = parameters.Take,
                ForSaleCategory = parameters.ForSaleCategory,
                ForRentCategory = parameters.ForRentCategory,
                JobCategory = parameters.JobCategory,
                ServiceCategory = parameters.ServiceCategory,
                Tags = parameters.Tags != null ? new List<string>(parameters.Tags) : new List<string>(),
                Field = parameters.PrimaryFilterField ?? string.Empty,
                Operator = FilterOperator.Equal,
                Value = parameters.PrimaryFilterValue ?? string.Empty
            };
        }


    }

    /// <summary>
    /// A view model for paginated data that includes filter information
    /// </summary>
    public class PaginatedViewModel<T>
    {
        public List<T> Items { get; set; } = new List<T>(); // Initialize to avoid null
        public int TotalItems { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage { get; set; }
        public bool HasNextPage { get; set; }
        public FilterParameters? AppliedFilters { get; set; }
    }
}