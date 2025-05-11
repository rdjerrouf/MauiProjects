using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models;
using MarketDZ.Services.Items;
using MarketDZ.Services.Media;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace MarketDZ.Services.Application.Items.Implementations
{
    public class PhotoServiceEnhanced : IItemPhotoService
    {
        private readonly IItemPhotoRepository _photoRepository;
        private readonly IMediaService _mediaService;
        private readonly ILogger<PhotoServiceEnhanced> _logger;

        public PhotoServiceEnhanced(
            IItemPhotoRepository photoRepository,
            IMediaService mediaService,
            ILogger<PhotoServiceEnhanced> logger)
        {
            _photoRepository = photoRepository ?? throw new ArgumentNullException(nameof(photoRepository));
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Batch upload photos with bulk operation support
        /// </summary>
        public async Task<bool> AddBulkPhotosToItemAsync(string itemId, string userId, ICollection<FileResult> photos)
        {
            try
            {
                var uploadTasks = new List<Task<(bool success, string path, string url)>>();

                // Process each photo in parallel
                foreach (var photo in photos)
                {
                    uploadTasks.Add(ProcessPhotoUpload(photo));
                }

                var results = await Task.WhenAll(uploadTasks);

                // Create photo entries for successful uploads
                var successfulUploads = results.Where(r => r.success).ToList();

                if (successfulUploads.Any())
                {
                    // Collect photo IDs from successful uploads
                    var photoIds = successfulUploads.Select(upload => upload.path).ToList();

                    // Use the existing AddPhotosAsync method with the PhotoOperationResult pattern
                    var result = await _photoRepository.DeletePhotosAsync(userId, itemId, photoIds);
                    return result.IsValid;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk photo upload for item {ItemId}", itemId);
                return false;
            }
        }

        /// <summary>
        /// Batch delete photos
        /// </summary>
        public async Task<bool> DeleteBulkPhotosAsync(string userId, IEnumerable<string> photoIds)
        {
            try
            {
                // First get all the photos we need to delete
                var photos = new List<ItemPhoto>();
                foreach (var photoId in photoIds)
                {
                    var photo = await _photoRepository.GetByIdAsync(photoId);
                    if (photo != null)
                    {
                        photos.Add(photo);
                    }
                }

                if (!photos.Any())
                {
                    return false;
                }

                // Fixing CS0128: Ensure 'itemId' is declared only once in the scope
                var itemId = photos.FirstOrDefault()?.ItemId ?? string.Empty;
                if (string.IsNullOrEmpty(itemId))
                {
                    return false;
                }

                // Delete files from storage
                var deleteTasks = photos.Select(async photo =>
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(photo.StoragePath))
                        {
                            await _mediaService.DeleteImageAsync(photo.StoragePath);
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting photo {PhotoId} from storage", photo.Id);
                        return false;
                    }
                });

                await Task.WhenAll(deleteTasks);

                // Use the existing DeletePhotosAsync method with the PhotoOperationResult pattern
                var result = await _photoRepository.DeletePhotosAsync(userId, itemId, photoIds.ToList());
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk photo deletion");
                return false;
            }
        }

        /// <summary>
        /// Processes a single photo upload
        /// </summary>
        private async Task<(bool success, string path, string url)> ProcessPhotoUpload(FileResult photo)
        {
            try
            {
                using var stream = await photo.OpenReadAsync();
                var result = await _mediaService.UploadImageAsync(stream, photo.FileName);
                return (true, result.storagePath, result.downloadUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing photo upload: {FileName}", photo.FileName);
                return (false, string.Empty, string.Empty);
            }
        }

        #region IItemPhotoService Implementation

        public async Task<ItemPhoto?> AddItemPhotoAsync(string userId, string itemId, FileResult photoFile)
        {
            try
            {
                var processResult = await ProcessPhotoUpload(photoFile);
                if (!processResult.success)
                {
                    return null;
                }

                var result = await _photoRepository.AddPhotoAsync(userId, itemId, photoFile);
                return result.Photo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding photo for item {ItemId}", itemId);
                return null;
            }
        }

        public async Task<bool> AddItemPhotoAsync(string userId, string itemId, string photoUrl)
        {
            try
            {
                var result = await _photoRepository.AddPhotoUrlAsync(userId, itemId, photoUrl);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding photo URL for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<List<ItemPhoto>> AddItemPhotosAsync(string userId, string itemId, IEnumerable<FileResult> photoFiles)
        {
            try
            {
                var result = await _photoRepository.AddPhotosAsync(userId, itemId, photoFiles.ToList());
                return result.Photos ?? new List<ItemPhoto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding multiple photos for item {ItemId}", itemId);
                return new List<ItemPhoto>();
            }
        }

        public async Task<bool> AddPhotosToItemAsync(string itemId, string userId, ICollection<FileResult> photos)
        {
            try
            {
                var result = await _photoRepository.AddPhotosAsync(userId, itemId, photos);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding photos to item {ItemId}", itemId);
                return false;
            }
        }

        public async Task DeletePhotosAsync(string postedByUserId, string itemId, List<string> photoIds)
        {
            try
            {
                await _photoRepository.DeletePhotosAsync(postedByUserId, itemId, photoIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting photos for item {ItemId}", itemId);
            }
        }

        public async Task<List<ItemPhoto>> GetAllItemPhotosAsync()
        {
            try
            {
                return await _photoRepository.GetAllAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all photos");
                return new List<ItemPhoto>();
            }
        }

        public async Task<List<ItemPhoto>> GetItemPhotosAsync(string itemId)
        {
            try
            {
                return await _photoRepository.GetByItemIdAsync(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting photos for item {ItemId}", itemId);
                return new List<ItemPhoto>();
            }
        }

        public async Task<bool> RemoveItemPhotoAsync(string userId, string photoId)
        {
            try
            {
                var photo = await _photoRepository.GetByIdAsync(photoId);
                if (photo != null && !string.IsNullOrEmpty(photo.StoragePath))
                {
                    try
                    {
                        await _mediaService.DeleteImageAsync(photo.StoragePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting photo {PhotoId} from storage", photoId);
                    }
                }

                var result = await _photoRepository.DeletePhotoAsync(userId, photoId);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing photo {PhotoId}", photoId);
                return false;
            }
        }

        public async Task<bool> ReorderItemPhotosAsync(string userId, string itemId, List<string> photoIds)
        {
            try
            {
                var result = await _photoRepository.ReorderPhotosAsync(userId, itemId, photoIds);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering photos for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> ReorderPhotosAsync(string itemId, List<string> photoIds)
        {
            try
            {
                // Get the first photo to check permissions
                var photos = await _photoRepository.GetByItemIdAsync(itemId);
                if (!photos.Any())
                {
                    return false;
                }

                // Since we don't have the userId in the ItemPhoto model,
                // we'll use the owner of the item ID passed in the method
                // Get the owner from the first photo or use a method to determine item ownership
                string ownerId = await GetItemOwnerIdAsync(itemId);
                if (string.IsNullOrEmpty(ownerId))
                {
                    return false;
                }

                var result = await _photoRepository.ReorderPhotosAsync(ownerId, itemId, photoIds);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering photos for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task<bool> SetPrimaryPhotoAsync(string userId, string photoId)
        {
            try
            {
                var result = await _photoRepository.SetPrimaryPhotoAsync(userId, photoId);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary photo {PhotoId}", photoId);
                return false;
            }
        }

        public async Task<bool> UpdateItemPhotoMetadataAsync(string itemId, string userId, List<string> photoUrls)
        {
            try
            {
                // For updating metadata, we can use AddPhotoUrlsAsync
                var result = await _photoRepository.AddPhotoUrlsAsync(userId, itemId, photoUrls);
                return result.IsValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating photo metadata for item {ItemId}", itemId);
                return false;
            }
        }

        public async Task UpdateItemPhotoMetadataAsync(string itemId, string userId, ICollection<string> photoUrls)
        {
            try
            {
                await _photoRepository.AddPhotoUrlsAsync(userId, itemId, photoUrls);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating photo metadata for item {ItemId}", itemId);
            }
        }

        // Helper method to determine item ownership - updated to return string
        private async Task<string> GetItemOwnerIdAsync(string itemId)
        {
            // This is a placeholder implementation
            // You should replace this with code that retrieves the user ID of the item owner
            // For example, by looking up the item in your database or using another service

            // Implementation example:
            // 1. If you have an IItemRepository that can get item details including the owner
            // return await _itemRepository.GetItemOwnerIdAsync(itemId);

            // 2. If the information is in your photos, you might implement a workaround:
            // var photos = await _photoRepository.GetByItemIdAsync(itemId);
            // var firstPhoto = photos.FirstOrDefault();
            // if (firstPhoto != null && CanGetOwnerFromPhoto(firstPhoto))
            // {
            //     return ExtractOwnerIdFromPhoto(firstPhoto);
            // }

            // For testing purposes, returning an empty string
            return string.Empty; // Replace with actual implementation
        }

        #endregion
    }
}