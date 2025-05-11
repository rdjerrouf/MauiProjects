using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Dtos.Item;
using MarketDZ.Models.Filters;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemSearchService
    {
        /// <summary> Searches items by text, optionally filtering by category. </summary> //
        Task<ObservableCollection<Item>> SearchItemsAsync(string searchTerm, string? category = null);

        /// <summary> Searches items by state. </summary> //
        Task<ObservableCollection<Item>> SearchByStateAsync(AlState state);

        /// <summary> Searches items by geographical location (latitude, longitude, radius). </summary> //
        Task<ObservableCollection<Item>> SearchByLocationAsync(double latitude, double longitude, double radiusKm);

        /// <summary> Searches items filtering by both category and state. </summary> //
        Task<ObservableCollection<Item>> SearchByCategoryAndStateAsync(string category, AlState state);

        /// <summary> Retrieves items based on a complex FilterCriteria object. </summary> //
        Task<ObservableCollection<Item>> GetItemsWithFiltersAsync(FilterCriteria criteria);

        /// <summary> Retrieves all items as a collection of ItemListDto. </summary> //
        Task<ObservableCollection<ItemListDto>> GetItemListDtosAsync();

        /// <summary> Retrieves filtered items (using FilterCriteria) as a collection of ItemListDto. </summary> //
        Task<ObservableCollection<ItemListDto>> GetItemsWithFiltersDtosAsync(FilterCriteria criteria);

        /// <summary> Retrieves items for a specific user as a collection of ItemListDto. </summary> //
        Task<ObservableCollection<ItemListDto>> GetUserItemListDtosAsync(string userId);

        /// <summary> Retrieves items based on simpler FilterParameters. </summary> //
        Task<List<Item>> GetFilteredItemsAsync(FilterParameters filter);

        /// <summary>
        /// Searches for items using advanced filters including tags and category-specific filters.
        /// </summary>
        Task<ObservableCollection<Item>> SearchWithAdvancedFiltersAsync(FilterCriteria criteria);

        /// <summary>
        /// Searches for items near a location with additional category-specific filters.
        /// </summary>
        Task<ObservableCollection<Item>> SearchNearbyWithCategoryFiltersAsync(
            double latitude,
            double longitude,
            double radiusKm,
            ItemCategory category,
            ForSaleCategory? forSaleCategory = null,
            ForRentCategory? forRentCategory = null,
            JobCategory? jobCategory = null,
            ServiceCategory? serviceCategory = null);

        /// <summary>
        /// Searches for items by tags with optional category filter.
        /// </summary>
        Task<ObservableCollection<Item>> SearchByTagsAsync(List<string> tags, string? category = null);

        /// <summary>
        /// Searches for items by date range with optional category filter.
        /// </summary>
        Task<ObservableCollection<Item>> SearchByDateRangeAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? category = null);

        /// <summary>
        /// Converts a list of items to list DTOs.
        /// </summary>
        ObservableCollection<ItemListDto> ToItemListDtos(IEnumerable<Item> items);
    }
}