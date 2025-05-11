using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Extensions;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Dtos.Item;
using MarketDZ.Models.Filters;
using MarketDZ.Services.Application.Items.Iterfaces;
using MarketDZ.Services.Core.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Items.Implementations
{
    /// <summary>
    /// Handles searching, filtering, and retrieving lists of items, including DTO transformations.
    /// </summary>
    public class ItemSearchService : IItemSearchService
    {
        private readonly IItemRepository _itemRepository;
        private readonly IItemCoreService _itemCoreService;
        private readonly ILogger<ItemSearchService> _logger;

        public ItemSearchService(
            IItemRepository itemRepository,
            IItemCoreService itemCoreService,
            ILogger<ItemSearchService> logger)
        {
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _itemCoreService = itemCoreService ?? throw new ArgumentNullException(nameof(itemCoreService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Item Search and Filters

        /// <summary>
        /// Search items by text and optional category. Returns active items only.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchItemsAsync(string searchText, string? category = null)
        {
            try
            {
                // First search by text
                var items = await _itemRepository.SearchByTextAsync(searchText);

                // Apply category filter if specified
                if (!string.IsNullOrEmpty(category))
                {
                    items = items.Where(i => i.Category.ToString() == category);
                }

                return new ObservableCollection<Item>(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching items with text '{searchText}' and category '{category}'");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Search items by category and state using ItemCategory enum. Returns active items sorted by newest date.
        /// This is an additional method not in the interface.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByCategoryAndStateAsync(ItemCategory category, AlState state)
        {
            try
            {
                // Convert enum to string and call the interface method
                return await SearchByCategoryAndStateAsync(category.ToString(), state);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching by category '{category}' and state '{state}'");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Search items by location (latitude, longitude, radius). Returns active items sorted by distance.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByLocationAsync(double latitude, double longitude, double radiusKm)
        {
            try
            {
                var filter = new FilterParameters
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusKm = radiusKm,
                    Status = ItemStatus.Active,
                    SortByDistance = true
                };
                var filteredItems = await _itemRepository.GetFilteredAsync(filter);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching by location ({latitude}, {longitude}, {radiusKm}km): {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Search items by category and state. Returns active items sorted by newest date.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByCategoryAndStateAsync(string category, AlState state)
        {
            try
            {
                var filter = new FilterParameters
                {
                    Category = category,
                    State = state,
                    Status = ItemStatus.Active,
                    SortBy = SortOption.DateNewest
                };
                var filteredItems = await _itemRepository.GetFilteredAsync(filter);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching by category '{category}' and state '{state}': {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Search items by states
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByStateAsync(AlState state)
        {
            try
            {
                var items = await _itemRepository.GetByStateAsync(state);
                return new ObservableCollection<Item>(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching items by state {state}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Get items using a complex FilterCriteria object.
        /// </summary>
        public async Task<ObservableCollection<Item>> GetItemsWithFiltersAsync(FilterCriteria criteria)
        {
            try
            {
                var filteredItems = await _itemRepository.GetFilteredByCriteriaAsync(criteria);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error filtering items with criteria: {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Get item list DTOs for *all* items. Uses IItemCoreService to get the base list.
        /// </summary>
        public async Task<ObservableCollection<ItemListDto>> GetItemListDtosAsync()
        {
            try
            {
                // Get base items from the core service
                var items = await _itemCoreService.GetItemsAsync();
                return new ObservableCollection<ItemListDto>(
                    items.Select(item => ((Item)item).ToListDto()!).Where(dto => dto != null)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving all item DTOs: {ex.Message}");
                return new ObservableCollection<ItemListDto>();
            }
        }

        /// <summary>
        /// Get filtered item list DTOs using FilterCriteria.
        /// </summary>
        public async Task<ObservableCollection<ItemListDto>> GetItemsWithFiltersDtosAsync(FilterCriteria criteria)
        {
            try
            {
                // Get filtered items using a method within this service
                var items = await GetItemsWithFiltersAsync(criteria);
                return new ObservableCollection<ItemListDto>(
                    items.Select(item => ((Item)item).ToListDto()!).Where(dto => dto != null)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error filtering item DTOs with criteria: {ex.Message}");
                return new ObservableCollection<ItemListDto>();
            }
        }

        /// <summary>
        /// Get user item list DTOs. Uses IItemCoreService to get the base user list.
        /// </summary>
        public async Task<ObservableCollection<ItemListDto>> GetUserItemListDtosAsync(string userId)
        {
            try
            {
                // Get base user items from the core service
                var items = await _itemCoreService.GetUserItemsAsync(userId);

                // Convert to DTOs
                return new ObservableCollection<ItemListDto>(
                    items.Select(item => ((Item)item).ToListDto()!).Where(dto => dto != null)
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving user item DTOs for user {userId}: {ex.Message}");
                return new ObservableCollection<ItemListDto>();
            }
        }

        /// <summary>
        /// Get filtered items using simpler FilterParameters.
        /// </summary>
        public async Task<List<Item>> GetFilteredItemsAsync(FilterParameters filter)
        {
            try
            {
                var items = await _itemRepository.GetFilteredAsync(filter);
                return items.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting filtered items with parameters: {ex.Message}");
                return new List<Item>();
            }
        }



        /// <summary>
        /// Searches for items using advanced filters including tags and category-specific filters.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchWithAdvancedFiltersAsync(FilterCriteria criteria)
        {
            try
            {
                // Use the existing repository method that works with FilterCriteria
                var filteredItems = await _itemRepository.GetFilteredByCriteriaAsync(criteria);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching with advanced filters: {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Searches for items near a location with additional category-specific filters.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchNearbyWithCategoryFiltersAsync(
            double latitude,
            double longitude,
            double radiusKm,
            ItemCategory category,
            ForSaleCategory? forSaleCategory = null,
            ForRentCategory? forRentCategory = null,
            JobCategory? jobCategory = null,
            ServiceCategory? serviceCategory = null)
        {
            try
            {
                // Create filter parameters with the new category-specific filters
                var filter = new FilterParameters
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    RadiusKm = radiusKm,
                    Category = category.ToString(),
                    Status = ItemStatus.Active,
                    SortByDistance = true
                };

                // Add the appropriate category-specific filter based on the main category
                switch (category)
                {
                    case ItemCategory.ForSale when forSaleCategory.HasValue:
                        filter.ForSaleCategory = forSaleCategory;
                        break;
                    case ItemCategory.ForRent when forRentCategory.HasValue:
                        filter.ForRentCategory = forRentCategory;
                        break;
                    case ItemCategory.Job when jobCategory.HasValue:
                        filter.JobCategory = jobCategory;
                        break;
                    case ItemCategory.Service when serviceCategory.HasValue:
                        filter.ServiceCategory = serviceCategory;
                        break;
                }

                var filteredItems = await _itemRepository.GetFilteredAsync(filter);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching nearby with category filters: {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Searches for items by tags with optional category filter.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByTagsAsync(List<string> tags, string? category = null)
        {
            try
            {
                // Create filter parameters with tags
                var filter = new FilterParameters
                {
                    Tags = tags,
                    Status = ItemStatus.Active,
                    SortBy = SortOption.DateNewest
                };

                if (!string.IsNullOrEmpty(category))
                {
                    filter.Category = category;
                }

                var filteredItems = await _itemRepository.GetFilteredAsync(filter);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching by tags: {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Searches for items by date range with optional category filter.
        /// </summary>
        public async Task<ObservableCollection<Item>> SearchByDateRangeAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? category = null)
        {
            try
            {
                // Create filter parameters with date range
                var filter = new FilterParameters
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    Status = ItemStatus.Active,
                    SortBy = SortOption.DateNewest
                };

                if (!string.IsNullOrEmpty(category))
                {
                    filter.Category = category;
                }

                var filteredItems = await _itemRepository.GetFilteredAsync(filter);
                return new ObservableCollection<Item>(filteredItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching by date range: {ex.Message}");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Converts a list of items to list DTOs.
        /// </summary>
        public ObservableCollection<ItemListDto> ToItemListDtos(IEnumerable<Item> items)
        {
            return new ObservableCollection<ItemListDto>(
                items.Select(item => item.ToListDto()!).Where(dto => dto != null)
            );
        }




        #endregion
    }
}