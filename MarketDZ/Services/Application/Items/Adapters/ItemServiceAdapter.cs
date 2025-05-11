using Microsoft.Extensions.Logging;
using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Filters;
using System.Collections.ObjectModel;
using MarketDZ.Services.Application.Items.Iterfaces;
using MarketDZ.Services.Application.Location.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos.Item;

namespace MarketDZ.Services.Application.Items.Adapters
{
    /// <summary>
    /// Adapter implementing IItemService using the specialized service interfaces.
    /// Provides backward compatibility during migration.
    /// </summary>
    public class ItemServiceAdapter 
    {
        private readonly IItemCoreService _coreService;
        private readonly IItemPhotoService _photoService;
        private readonly IItemSearchService _searchService;
        private readonly IItemStatisticsService _statsService;
        private readonly ILogger<ItemServiceAdapter> _logger;

        public ItemServiceAdapter(
            IItemCoreService coreService,
            IItemPhotoService photoService,
            IItemSearchService searchService,
            IItemStatisticsService statsService,
            ILogger<ItemServiceAdapter> logger)
        {
            _coreService = coreService ?? throw new ArgumentNullException(nameof(coreService));
            _photoService = photoService ?? throw new ArgumentNullException(nameof(photoService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _statsService = statsService ?? throw new ArgumentNullException(nameof(statsService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Item CRUD Operations

        public async Task<bool> AddItemAsync(Item item)
        {
            return await _coreService.AddItemAsync(item);
        }

        public async Task<string?> AddForSaleItemAsync(string userId, CreateForSaleItemDto itemDto)
        {
            var itemId = await _coreService.AddForSaleItemAsync(userId, itemDto);

            // Add photos if necessary
            if (!string.IsNullOrEmpty(itemId))
            {
                if (itemDto.Photos?.Any() == true)
                {
                    await _photoService.AddPhotosToItemAsync(itemId, userId, itemDto.Photos);
                }

                if (itemDto.PhotoUrls?.Any() == true)
                {
                    foreach (var url in itemDto.PhotoUrls)
                    {
                        await _photoService.AddItemPhotoAsync(userId, itemId, url);
                    }
                }
            }

            return itemId;
        }

        public async Task<string?> AddRentalItemAsync(string userId, CreateRentalItemDto itemDto)
        {
            var itemId = await _coreService.AddRentalItemAsync(userId, itemDto);

            // Add photos if necessary
            if (!string.IsNullOrEmpty(itemId))
            {
                if (itemDto.Photos?.Any() == true)
                {
                    await _photoService.AddPhotosToItemAsync(itemId, userId, itemDto.Photos);
                }

                if (itemDto.PhotoUrls?.Any() == true)
                {
                    foreach (var url in itemDto.PhotoUrls)
                    {
                        await _photoService.AddItemPhotoAsync(userId, itemId, url);
                    }
                }
            }

            return itemId;
        }

        public async Task<string?> AddJobItemAsync(string userId, CreateJobItemDto itemDto)
        {
            return await _coreService.AddJobItemAsync(userId, itemDto);
        }

        public async Task<string?> AddServiceItemAsync(string userId, CreateServiceItemDto itemDto)
        {
            return await _coreService.AddServiceItemAsync(userId, itemDto);
        }

        public async Task<string?> AddItemAsync(string userId, CreateItemDto itemDto)
        {
            // This method is being removed as discussed,
            // but we'll still need to handle it for compatibility

            // Handle different types of DTOs, similar to how the original implementation did
            if (itemDto is CreateForSaleItemDto forSaleDto)
            {
                return await AddForSaleItemAsync(userId, forSaleDto);
            }
            else if (itemDto is CreateRentalItemDto rentalDto)
            {
                return await AddRentalItemAsync(userId, rentalDto);
            }
            else if (itemDto is CreateJobItemDto jobDto)
            {
                return await AddJobItemAsync(userId, jobDto);
            }
            else if (itemDto is CreateServiceItemDto serviceDto)
            {
                return await AddServiceItemAsync(userId, serviceDto);
            }
            else
            {
                _logger.LogWarning("Unsupported item DTO type in generic AddItemAsync");
                return null;
            }
        }

        public async Task<bool> UpdateItemAsync(string userId, string itemId, ItemUpdateDto updateDto)
        {
            // Update the item itself
            bool updated = await _coreService.UpdateItemAsync(userId, itemId, updateDto);

            // Handle photo updates if successful
            if (updated && (updateDto.Photos?.Any() == true || updateDto.PhotoUrls?.Any() == true))
            {
                if (updateDto.Photos?.Any() == true)
                {
                    await _photoService.AddPhotosToItemAsync(itemId, userId, updateDto.Photos);
                }

                if (updateDto.PhotoUrls?.Any() == true)
                {
                    await _photoService.UpdateItemPhotoMetadataAsync(itemId, userId, updateDto.PhotoUrls);
                }
            }

            return updated;
        }

        public async Task<bool> DeleteItemAsync(string id)
        {
            // Get the item to check ownership
            var item = await _coreService.GetItemAsync(id);
            if (item == null)
            {
                return false;
            }

            // Delete photos first
            var photos = await _photoService.GetItemPhotosAsync(id);
            if (photos.Any())
            {
                await _photoService.DeletePhotosAsync(item.PostedByUserId, id, photos.Select(p => p.Id).ToList());
            }

            // Then delete the item
            return await _coreService.DeleteItemAsync(id);
        }

        public async Task<Item?> GetItemAsync(string id)
        {
            var item = await _coreService.GetItemAsync(id);
            if (item != null)
            {
                // Increment view count
                await _statsService.IncrementItemViewAsync(id);
            }
            return item;
        }

        public async Task<ObservableCollection<Item>> GetItemsAsync()
        {
            return await _coreService.GetItemsAsync();
        }

        public async Task<ObservableCollection<Item>> GetUserItemsAsync(string userId)
        {
            return (ObservableCollection<Item>)await _coreService.GetUserItemsAsync(userId);
        }

        public async Task<ObservableCollection<Item>> GetItemsByUserAsync(string userId)
        {
            return await _coreService.GetItemsByUserAsync(userId);
        }

        #endregion

        #region Item Search and Filters

        public async Task<ObservableCollection<Item>> SearchItemsAsync(string searchTerm, string? category = null)
        {
            return await _searchService.SearchItemsAsync(searchTerm, category);
        }

        public async Task<ObservableCollection<Item>> SearchByStateAsync(AlState state)
        {
            return await _searchService.SearchByStateAsync(state);
        }

        public async Task<ObservableCollection<Item>> SearchByLocationAsync(double latitude, double longitude, double radiusKm)
        {
            return await _searchService.SearchByLocationAsync(latitude, longitude, radiusKm);
        }

        public async Task<ObservableCollection<Item>> SearchByCategoryAndStateAsync(string category, AlState state)
        {
            return await _searchService.SearchByCategoryAndStateAsync(category, state);
        }

        public async Task<ObservableCollection<Item>> GetItemsWithFiltersAsync(FilterCriteria criteria)
        {
            return await _searchService.GetItemsWithFiltersAsync(criteria);
        }

        public async Task<ObservableCollection<ItemListDto>> GetItemListDtosAsync()
        {
            return await _searchService.GetItemListDtosAsync();
        }

        public async Task<ObservableCollection<ItemListDto>> GetItemsWithFiltersDtosAsync(FilterCriteria criteria)
        {
            return await _searchService.GetItemsWithFiltersDtosAsync(criteria);
        }

        public async Task<ObservableCollection<ItemListDto>> GetUserItemListDtosAsync(string userId)
        {
            return await _searchService.GetUserItemListDtosAsync(userId);
        }

        public async Task<List<Item>> GetFilteredItemsAsync(FilterParameters filter)
        {
            return await _searchService.GetFilteredItemsAsync(filter);
        }

        #endregion

        #region Favorites and Ratings

        public async Task<bool> AddFavoriteAsync(string userId, string itemId)
        {
            return await _statsService.AddFavoriteAsync(userId, itemId);
        }

        public async Task<bool> RemoveFavoriteAsync(string userId, string itemId)
        {
            return await _statsService.RemoveFavoriteAsync(userId, itemId);
        }

        public async Task<ObservableCollection<Item>> GetUserFavoriteItemsAsync(string userId)
        {
            return await _statsService.GetUserFavoriteItemsAsync(userId);
        }

        public async Task<bool> AddRatingAsync(string userId, string itemId, int score, string review)
        {
            return await _statsService.AddRatingAsync(userId, itemId, score, review);
        }

        public async Task<IEnumerable<Rating>> GetUserRatingsAsync(string userId)
        {
            return await _statsService.GetUserRatingsAsync(userId);
        }

        public async Task<IEnumerable<Rating>> GetItemRatingsAsync(string itemId)
        {
            return await _statsService.GetItemRatingsAsync(itemId);
        }

        #endregion

        #region Statistics and Metrics

        public async Task<UserProfileStatistics> GetUserProfileStatisticsAsync(string userId)
        {
            return await _statsService.GetUserProfileStatisticsAsync(userId);
        }

        public async Task<IEnumerable<Rating>> GetUserItemRatingsAsync(string userId)
        {
            return await _statsService.GetUserItemRatingsAsync(userId);
        }

        public async Task<IEnumerable<ItemPerformanceDto>> GetTopPerformingItemsAsync(int count)
        {
            return await _statsService.GetTopPerformingItemsAsync(count);
        }

        public async Task<ItemStatistics?> GetItemStatisticsAsync(string itemId)
        {
            return await _statsService.GetItemStatisticsAsync(itemId);
        }

        public async Task<bool> IncrementItemViewAsync(string itemId)
        {
            return await _statsService.IncrementItemViewAsync(itemId);
        }

        public async Task<bool> RecordItemInquiryAsync(string itemId)
        {
            return await _statsService.RecordItemInquiryAsync(itemId);
        }

        public async Task<bool> UpdateItemStatusAsync(string userId, string itemId, ItemStatus status)
        {
            return await _coreService.UpdateItemStatusAsync(userId, itemId, status);
        }

        public async Task<bool> IsItemAvailableAsync(string itemId)
        {
            return await _coreService.IsItemAvailableAsync(itemId);
        }

        #endregion

        #region Photo Operations

        public async Task<bool> ReorderPhotosAsync(string itemId, List<string> photoIds)
        {
            // Get the item to determine its owner
            var item = await _coreService.GetItemAsync(itemId);
            if (item == null)
            {
                return false;
            }

            return await _photoService.ReorderPhotosAsync(itemId, photoIds);
        }

        public async Task<bool> AddPhotosToItemAsync(string itemId, string userId, ICollection<FileResult> photos)
        {
            return await _photoService.AddPhotosToItemAsync(itemId, userId, photos);
        }

        public async Task<ItemPhoto?> AddItemPhotoAsync(string userId, string itemId, FileResult photoFile)
        {
            return await _photoService.AddItemPhotoAsync(userId, itemId, photoFile);
        }

        public async Task<bool> AddItemPhotoAsync(string userId, string itemId, string photoUrl)
        {
            return await _photoService.AddItemPhotoAsync(userId, itemId, photoUrl);
        }

        public async Task<bool> RemoveItemPhotoAsync(string userId, string photoId)
        {
            return await _photoService.RemoveItemPhotoAsync(userId, photoId);
        }

        public async Task<bool> UpdateItemPhotoMetadataAsync(string itemId, string userId, List<string> photoUrls)
        {
            return await _photoService.UpdateItemPhotoMetadataAsync(itemId, userId, photoUrls);
        }

        public async Task<List<ItemPhoto>> GetItemPhotosAsync(string itemId)
        {
            return await _photoService.GetItemPhotosAsync(itemId);
        }

        public async Task<bool> SetPrimaryPhotoAsync(string userId, string photoId)
        {
            return await _photoService.SetPrimaryPhotoAsync(userId, photoId);
        }

        public async Task<bool> ReorderItemPhotosAsync(string userId, string itemId, List<string> photoIds)
        {
            return await _photoService.ReorderItemPhotosAsync(userId, itemId, photoIds);
        }

        public async Task<List<ItemPhoto>> GetAllItemPhotosAsync()
        {
            return await _photoService.GetAllItemPhotosAsync();
        }

        public async Task<List<ItemPhoto>> AddItemPhotosAsync(string userId, string itemId, IEnumerable<FileResult> photoFiles)
        {
            return await _photoService.AddItemPhotosAsync(userId, itemId, photoFiles);
        }

        #endregion
    }
}