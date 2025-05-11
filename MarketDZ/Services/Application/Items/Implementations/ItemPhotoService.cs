using MarketDZ.Models; // Assuming ItemPhoto is here
using MarketDZ.Services.Repositories; // Assuming IItemPhotoRepository and IItemRepository are here
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage; // Required for FileResult

namespace MarketDZ.Services.Application.Items.Implementations
{
    /// <summary>
    /// Handles management of photos associated with items.
    /// </summary>
    public class ItemPhotoService : IItemPhotoService
    {
        private readonly IItemPhotoRepository _photoRepository;
        private readonly IItemRepository _itemRepository; // Needed to verify item ownership sometimes
        private readonly ILogger<ItemPhotoService> _logger;

        public ItemPhotoService(
            IItemPhotoRepository photoRepository,
            IItemRepository itemRepository, // Add this dependency
            ILogger<ItemPhotoService> logger)
        {
            _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository)); // Initialize
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Photo Operations

        /// <summary>
        /// Reorder photos for an item. Verifies item existence first.
        /// </summary>
        public async Task<bool> ReorderPhotosAsync(string itemId, List<string> photoIds)
        {
            try
            {
                // Get the item to verify it exists and to potentially get the user ID if needed for repo call
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    _logger.LogError($"Cannot reorder photos: Item {itemId} not found");
                    return false;
                }

                // Assuming the repository method needs userId, pass item.PostedByUserId
                // Adjust if your repository method signature is different.
                var result = await _photoRepository.ReorderPhotosAsync(item.PostedByUserId, itemId, photoIds);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reordering photos for item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Add photos to item using a collection of FileResult.
        /// </summary>
        public async Task<bool> AddPhotosToItemAsync(string itemId, string userId, ICollection<FileResult> photos)
        {
            // This method seems slightly redundant with AddItemPhotosAsync below,
            // but implementing as per original interface definition.
            try
            {
                var result = await _photoRepository.AddPhotosAsync(userId, itemId, photos);
                if (!result.IsValid)
                {
                    _logger.LogError($"Failed to add photos to item {itemId}: {result.ErrorMessage}");
                }
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding multiple photos (ICollection) to item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Add a single photo to an item using FileResult.
        /// </summary>
        public async Task<ItemPhoto?> AddItemPhotoAsync(string userId, string itemId, FileResult photoFile)
        {
            try
            {
                var result = await _photoRepository.AddPhotoAsync(userId, itemId, photoFile);
                if (!result.IsValid)
                {
                    _logger.LogError($"Failed to add photo to item {itemId}: {result.ErrorMessage}");
                    return null;
                }
                return result.Photo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding single photo to item {itemId}");
                return null;
            }
        }

        /// <summary>
        /// Add a photo URL to an item. (Legacy - Consider migrating away from URL strings if possible)
        /// Verifies item ownership first.
        /// </summary>
        public async Task<bool> AddItemPhotoAsync(string userId, string itemId, string photoUrl)
        {
            try
            {
                _logger.LogWarning($"Using legacy AddItemPhotoAsync with URL for item {itemId}. Consider migrating to FileResult version.");
                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null || item.PostedByUserId != userId)
                {
                    _logger.LogError($"Cannot add photo URL: User {userId} is not the owner of item {itemId} or item not found.");
                    return false;
                }

                // Use the photo repository
                var result = await _photoRepository.AddPhotoUrlAsync(userId, itemId, photoUrl);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding photo URL to item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Remove a photo by its ID.
        /// </summary>
        public async Task<bool> RemoveItemPhotoAsync(string userId, string photoId)
        {
            try
            {
                // The repository method likely handles ownership verification based on userId and photoId
                var result = await _photoRepository.DeletePhotoAsync(userId, photoId);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing photo {photoId}");
                return false;
            }
        }

        /// <summary>
        /// Update photo metadata using URLs. (Legacy - Tied to URL-based photo adding)
        /// Verifies item ownership.
        /// </summary>
        public async Task<bool> UpdateItemPhotoMetadataAsync(string itemId, string userId, List<string> photoUrls)
        {
            try
            {
                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null || item.PostedByUserId != userId)
                {
                    _logger.LogError($"Cannot update photo metadata: Item not found or user {userId} is not the owner of item {itemId}");
                    return false;
                }

                // This likely implies adding URLs that weren't previously tracked.
                // Consider if this logic is still needed or if it should be part of a different flow.
                // The original implementation called AddPhotoUrlAsync for each URL.
                _logger.LogWarning($"Executing legacy UpdateItemPhotoMetadataAsync for item {itemId}. Review if this logic is still required.");
                foreach (var photoUrl in photoUrls)
                {
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        await _photoRepository.AddPhotoUrlAsync(userId, itemId, photoUrl);
                    }
                }

                return true; // Assuming success if loop completes without error
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item photo metadata for item {itemId}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get all photos for a specific item.
        /// </summary>
        public async Task<List<ItemPhoto>> GetItemPhotosAsync(string itemId)
        {
            try
            {
                return await _photoRepository.GetByItemIdAsync(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting photos for item {itemId}");
                return new List<ItemPhoto>(); // Return empty list on error
            }
        }

        /// <summary>
        /// Set a specific photo as the primary photo for its item.
        /// </summary>
        public async Task<bool> SetPrimaryPhotoAsync(string userId, string photoId)
        {
            try
            {
                // Repository method likely handles ownership verification via userId and photoId
                var result = await _photoRepository.SetPrimaryPhotoAsync(userId, photoId);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting primary photo for photoId {photoId}");
                return false;
            }
        }

        /// <summary>
        /// Reorder item photos. (Functionally identical to ReorderPhotosAsync)
        /// Verifies item existence.
        /// </summary>
        public async Task<bool> ReorderItemPhotosAsync(string userId, string itemId, List<string> photoIds)
        {
            // This appears redundant with ReorderPhotosAsync. Calling that implementation.
            _logger.LogDebug($"Calling ReorderPhotosAsync from ReorderItemPhotosAsync for item {itemId}");
            return await ReorderPhotosAsync(itemId, photoIds);
        }

        /// <summary>
        /// Get all photos across all items.
        /// </summary>
        public async Task<List<ItemPhoto>> GetAllItemPhotosAsync()
        {
            try
            {
                return await _photoRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all item photos");
                return new List<ItemPhoto>();
            }
        }

        /// <summary>
        /// Add multiple photos to an item using IEnumerable of FileResult.
        /// </summary>
        public async Task<List<ItemPhoto>> AddItemPhotosAsync(string userId, string itemId, IEnumerable<FileResult> photoFiles)
        {
            // This is very similar to AddPhotosToItemAsync but returns the list of photos.
            try
            {
                var result = await _photoRepository.AddPhotosAsync(userId, itemId, photoFiles.ToList()); // Convert IEnumerable to List if needed by repo method
                if (!result.IsValid)
                {
                    _logger.LogError($"Failed to add photos (IEnumerable): {result.ErrorMessage}");
                    return new List<ItemPhoto>();
                }

                // Return the updated list of photos for this item, as per original logic
                return await _photoRepository.GetByItemIdAsync(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding multiple photos (IEnumerable) to item {itemId}");
                return new List<ItemPhoto>();
            }
        }

        /// <summary>
        /// Update photo metadata using a collection of URLs.
        /// </summary>
        public async Task UpdateItemPhotoMetadataAsync(string itemId, string userId, ICollection<string> photoUrls)
        {
            try
            {
                // Similar implementation to the List<string> version
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null || item.PostedByUserId != userId)
                {
                    _logger.LogError($"Cannot update photo metadata: Item not found or user {userId} is not the owner of item {itemId}");
                    return;
                }

                _logger.LogWarning($"Executing UpdateItemPhotoMetadataAsync with ICollection for item {itemId}.");
                foreach (var photoUrl in photoUrls)
                {
                    if (!string.IsNullOrEmpty(photoUrl))
                    {
                        await _photoRepository.AddPhotoUrlAsync(userId, itemId, photoUrl);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item photo metadata with ICollection for item {itemId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete multiple photos by their IDs.
        /// </summary>
        public async Task DeletePhotosAsync(string postedByUserId, string id, List<string> photoIds)
        {
            try
            {
                foreach (var photoId in photoIds)
                {
                    await _photoRepository.DeletePhotoAsync(postedByUserId, photoId);
                }
                _logger.LogInformation($"Successfully deleted {photoIds.Count} photos for item {id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting photos for item {id}");
            }
        }

        #endregion
    }
}