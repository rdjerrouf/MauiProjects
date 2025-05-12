using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using MarketDZ.Services.Core.Interfaces.Cache;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Services.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Firebase-specific implementation of the Item Photo repository
    /// </summary>
    public class FirebaseItemPhotoRepository : BaseRepository<ItemPhoto, FirebaseItemPhoto>, IItemPhotoRepository
    {
        private readonly IFirebaseTransactionHelper _transactionHelper;

        public FirebaseItemPhotoRepository(
            IAppCoreDataStore dataStore,
            IEntityMapper<ItemPhoto, FirebaseItemPhoto> entityMapper,
            ILogger<FirebaseItemPhotoRepository> logger,
            ICacheService cacheService,
            IFirebaseTransactionHelper transactionHelper)
            : base(dataStore, entityMapper, logger, cacheService, "photos")
        {
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
        }

        /// <summary>
        /// Get all photos for an item
        /// </summary>
        public async Task<List<ItemPhoto>> GetByItemIdAsync(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return new List<ItemPhoto>();
            }

            string cacheKey = $"item_{itemId}_photos";

            // Try to get from cache
            if (CacheService != null && CacheService.TryGetFromCache<List<ItemPhoto>>(cacheKey, out var cachedPhotos))
            {
                return cachedPhotos;
            }

            try
            {
                // Get from item photos index
                var itemPhotosPath = $"item_photos/{itemId}";
                var photoEntries = await DataStore.GetCollectionAsync<Dictionary<string, object>>(itemPhotosPath);

                if (photoEntries == null || !photoEntries.Any())
                {
                    return new List<ItemPhoto>();
                }

                // Extract photo IDs and fetch full photos
                var photos = new List<ItemPhoto>();

                foreach (var entry in photoEntries)
                {
                    if (entry.ContainsKey("photoId"))
                    {
                        var photoId = entry["photoId"]?.ToString();
                        var photo = await GetByIdAsync(photoId);
                        if (photo != null)
                        {
                            photos.Add(photo);
                        }
                    }
                }

                // Sort by display order
                photos = photos.OrderBy(p => int.TryParse(p.DisplayOrder, out var order) ? order : int.MaxValue).ToList();

                // Cache the result
                CacheService?.AddToCache(cacheKey, photos, TimeSpan.FromMinutes(10));

                return photos;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error getting photos for item {itemId}");
                return new List<ItemPhoto>();
            }
        }

        /// <summary>
        /// Get all photos
        /// </summary>
        public async Task<List<ItemPhoto>> GetAllAsync()
        {
            return (await base.GetAllAsync(0, 1000)).ToList(); // Limit to prevent memory issues
        }

        /// <summary>
        /// Add a photo to an item from a file
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoAsync(string userId, string itemId, FileResult photoFile)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || photoFile == null)
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            try
            {
                // Check if user has permission
                if (!await CanUserManageItemAsync(userId, itemId))
                {
                    return PhotoOperationResult.Failure("User does not have permission to add photos to this item");
                }

                // Get current photos to determine order
                var existingPhotos = await GetByItemIdAsync(itemId);
                var nextOrder = existingPhotos.Count;

                // Create photo URL (in a real implementation, you'd upload to storage first)
                // This is a placeholder for the actual storage upload logic
                var photoUrl = $"https://storage.example.com/{itemId}/{Guid.NewGuid()}.jpg";

                var photo = new ItemPhoto
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = itemId,
                    PhotoUrl = photoUrl,
                    StoragePath = $"{itemId}/{Guid.NewGuid()}.jpg",
                    IsPrimaryPhoto = existingPhotos.Count == 0, // First photo is primary
                    DisplayOrder = nextOrder.ToString(),
                    UploadedAt = DateTime.UtcNow,
                    Caption = "",
                    Version = 1,
                    LastModified = DateTime.UtcNow
                };

                // Create the photo
                var photoId = await CreateAsync(photo);
                photo.Id = photoId;

                // Update item's primary photo if this is the first photo
                if (photo.IsPrimaryPhoto)
                {
                    var updates = new Dictionary<string, object>
                    {
                        ["photoUrl"] = photoUrl
                    };
                    await DataStore.UpdateEntityFieldsAsync($"items/{itemId}", updates);
                }

                // Invalidate caches
                InvalidatePhotoCaches(itemId);

                return PhotoOperationResult.Success(photo);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error adding photo to item {itemId}");
                return PhotoOperationResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Add multiple photos to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotosAsync(string userId, string itemId, ICollection<FileResult> photoFiles)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || photoFiles == null || !photoFiles.Any())
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            var addedPhotos = new List<ItemPhoto>();
            var errors = new List<string>();

            foreach (var photoFile in photoFiles)
            {
                var result = await AddPhotoAsync(userId, itemId, photoFile);

                if (result.IsValid && result.Photo != null)
                {
                    addedPhotos.Add(result.Photo);
                }
                else
                {
                    errors.Add(result.ErrorMessage);
                }
            }

            if (errors.Any() && !addedPhotos.Any())
            {
                return PhotoOperationResult.Failure(string.Join("; ", errors));
            }

            return PhotoOperationResult.Success(addedPhotos);
        }

        /// <summary>
        /// Add a photo URL to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoUrlAsync(string userId, string itemId, string photoUrl)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || string.IsNullOrEmpty(photoUrl))
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            try
            {
                // Check if user has permission
                if (!await CanUserManageItemAsync(userId, itemId))
                {
                    return PhotoOperationResult.Failure("User does not have permission to add photos to this item");
                }

                // Get current photos to determine order
                var existingPhotos = await GetByItemIdAsync(itemId);
                var nextOrder = existingPhotos.Count;

                var photo = new ItemPhoto
                {
                    Id = Guid.NewGuid().ToString(),
                    ItemId = itemId,
                    PhotoUrl = photoUrl,
                    IsPrimaryPhoto = existingPhotos.Count == 0, // First photo is primary
                    DisplayOrder = nextOrder.ToString(),
                    UploadedAt = DateTime.UtcNow,
                    Caption = "",
                    Version = 1,
                    LastModified = DateTime.UtcNow
                };

                // Create the photo
                var photoId = await CreateAsync(photo);
                photo.Id = photoId;

                // Update item's primary photo if this is the first photo
                if (photo.IsPrimaryPhoto)
                {
                    var updates = new Dictionary<string, object>
                    {
                        ["photoUrl"] = photoUrl
                    };
                    await DataStore.UpdateEntityFieldsAsync($"items/{itemId}", updates);
                }

                // Invalidate caches
                InvalidatePhotoCaches(itemId);

                return PhotoOperationResult.Success(photo);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error adding photo URL to item {itemId}");
                return PhotoOperationResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Add multiple photo URLs to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoUrlsAsync(string userId, string itemId, IEnumerable<string> photoUrls)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || photoUrls == null || !photoUrls.Any())
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            var addedPhotos = new List<ItemPhoto>();
            var errors = new List<string>();

            foreach (var photoUrl in photoUrls)
            {
                var result = await AddPhotoUrlAsync(userId, itemId, photoUrl);

                if (result.IsValid && result.Photo != null)
                {
                    addedPhotos.Add(result.Photo);
                }
                else
                {
                    errors.Add(result.ErrorMessage);
                }
            }

            if (errors.Any() && !addedPhotos.Any())
            {
                return PhotoOperationResult.Failure(string.Join("; ", errors));
            }

            return PhotoOperationResult.Success(addedPhotos);
        }

        /// <summary>
        /// Delete a photo
        /// </summary>
        public async Task<PhotoOperationResult> DeletePhotoAsync(string userId, string photoId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(photoId))
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            try
            {
                // Get the photo
                var photo = await GetByIdAsync(photoId);
                if (photo == null)
                {
                    return PhotoOperationResult.Failure("Photo not found");
                }

                // Check if user has permission
                if (!await CanUserManageItemAsync(userId, photo.ItemId))
                {
                    return PhotoOperationResult.Failure("User does not have permission to delete this photo");
                }

                // Delete the photo
                var result = await DeleteAsync(photoId);

                if (!result)
                {
                    return PhotoOperationResult.Failure("Failed to delete photo");
                }

                // If this was the primary photo, set another one
                if (photo.IsPrimaryPhoto)
                {
                    var remainingPhotos = await GetByItemIdAsync(photo.ItemId);
                    if (remainingPhotos.Any())
                    {
                        var newPrimary = remainingPhotos.First();
                        newPrimary.IsPrimaryPhoto = true;
                        await UpdatePhotoAsync(newPrimary);

                        // Update item's primary photo
                        var updates = new Dictionary<string, object>
                        {
                            ["photoUrl"] = newPrimary.PhotoUrl
                        };
                        await DataStore.UpdateEntityFieldsAsync($"items/{photo.ItemId}", updates);
                    }
                    else
                    {
                        // No photos left, clear the item's photo URL
                        var updates = new Dictionary<string, object>
                        {
                            ["photoUrl"] = null
                        };
                        await DataStore.UpdateEntityFieldsAsync($"items/{photo.ItemId}", updates);
                    }
                }

                // Invalidate caches
                InvalidatePhotoCaches(photo.ItemId);

                return PhotoOperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error deleting photo {photoId}");
                return PhotoOperationResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete multiple photos
        /// </summary>
        public async Task<PhotoOperationResult> DeletePhotosAsync(string userId, string itemId, IEnumerable<string> photoIds)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || photoIds == null || !photoIds.Any())
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            var errors = new List<string>();

            foreach (var photoId in photoIds)
            {
                var result = await DeletePhotoAsync(userId, photoId);

                if (!result.IsValid)
                {
                    errors.Add(result.ErrorMessage);
                }
            }

            if (errors.Any())
            {
                return PhotoOperationResult.Failure(string.Join("; ", errors));
            }

            return PhotoOperationResult.Success();
        }

        /// <summary>
        /// Set a photo as the primary photo for an item
        /// </summary>
        public async Task<PhotoOperationResult> SetPrimaryPhotoAsync(string userId, string photoId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(photoId))
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            try
            {
                // Get the photo
                var photo = await GetByIdAsync(photoId);
                if (photo == null)
                {
                    return PhotoOperationResult.Failure("Photo not found");
                }

                // Check if user has permission
                if (!await CanUserManageItemAsync(userId, photo.ItemId))
                {
                    return PhotoOperationResult.Failure("User does not have permission to manage this photo");
                }

                // Get all photos for the item
                var allPhotos = await GetByItemIdAsync(photo.ItemId);

                // Update primary flag
                foreach (var p in allPhotos)
                {
                    p.IsPrimaryPhoto = p.Id == photoId;
                    await UpdatePhotoAsync(p);
                }

                // Update item's primary photo
                var updates = new Dictionary<string, object>
                {
                    ["photoUrl"] = photo.PhotoUrl
                };
                await DataStore.UpdateEntityFieldsAsync($"items/{photo.ItemId}", updates);

                // Invalidate caches
                InvalidatePhotoCaches(photo.ItemId);

                return PhotoOperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error setting primary photo {photoId}");
                return PhotoOperationResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Reorder photos for an item
        /// </summary>
        public async Task<PhotoOperationResult> ReorderPhotosAsync(string userId, string itemId, List<string> photoIds)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(itemId) || photoIds == null || !photoIds.Any())
            {
                return PhotoOperationResult.Failure("Invalid parameters");
            }

            try
            {
                // Check if user has permission
                if (!await CanUserManageItemAsync(userId, itemId))
                {
                    return PhotoOperationResult.Failure("User does not have permission to manage photos for this item");
                }

                // Get all photos for the item
                var allPhotos = await GetByItemIdAsync(itemId);

                // Verify all photo IDs belong to this item
                var itemPhotoIds = allPhotos.Select(p => p.Id).ToHashSet();
                if (!photoIds.All(id => itemPhotoIds.Contains(id)))
                {
                    return PhotoOperationResult.Failure("One or more photo IDs do not belong to this item");
                }

                // Update display order
                for (int i = 0; i < photoIds.Count; i++)
                {
                    var photo = allPhotos.FirstOrDefault(p => p.Id == photoIds[i]);
                    if (photo != null)
                    {
                        photo.DisplayOrder = i.ToString();
                        await UpdatePhotoAsync(photo);
                    }
                }

                // Invalidate caches
                InvalidatePhotoCaches(itemId);

                return PhotoOperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error reordering photos for item {itemId}");
                return PhotoOperationResult.Failure($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Update photo metadata
        /// </summary>
        public async Task<bool> UpdatePhotoAsync(ItemPhoto photo)
        {
            if (photo == null)
            {
                throw new ArgumentNullException(nameof(photo));
            }

            return await UpdateAsync(photo);
        }

        /// <summary>
        /// Get the next available photo ID
        /// </summary>
        public async Task<string> GetNextPhotoIdAsync()
        {
            return Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Check if a user has permission to manage a photo
        /// </summary>
        public async Task<bool> CanUserManagePhotoAsync(string userId, string photoId)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(photoId))
            {
                return false;
            }

            try
            {
                // Get the photo
                var photo = await GetByIdAsync(photoId);
                if (photo == null)
                {
                    return false;
                }

                return await CanUserManageItemAsync(userId, photo.ItemId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error checking user permission for photo {photoId}");
                return false;
            }
        }

        /// <summary>
        /// Check if a user has permission to manage photos for an item (renamed from duplicate)
        /// </summary>
        private async Task<bool> CanUserManageItemAsync(string userId, string itemId)
        {
            try
            {
                // Get the item
                var item = await DataStore.GetEntityAsync<Dictionary<string, object>>($"items/{itemId}");

                if (item == null)
                {
                    return false;
                }

                // Check if user owns the item
                if (item.TryGetValue("postedByUserId", out var postedByUserId))
                {
                    return postedByUserId?.ToString() == userId;
                }

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Error checking user permission for item {itemId}");
                return false;
            }
        }

        /// <summary>
        /// Invalidates photo-related caches
        /// </summary>
        private void InvalidatePhotoCaches(string itemId)
        {
            if (CacheService == null)
            {
                return;
            }

            // Invalidate item photos cache
            CacheService.InvalidateCache($"item_{itemId}_photos");

            // Invalidate item cache
            CacheService.InvalidateCache($"items_{itemId}");
        }
    }
}