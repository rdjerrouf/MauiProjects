using MarketDZ.Models;
using MarketDZ.Models.Firebase.Base.Adapters;
using MarketDZ.Services.Media;
using Microsoft.Extensions.Logging;
using MarketDZ.Services.DbServices;
using MarketDZ.Services.Utils.Firebase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// Enhanced Firebase implementation of IItemPhotoRepository with string IDs
    /// </summary>
    public class FirebaseItemPhotoRepository : IItemPhotoRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IItemRepository _itemRepository;
        private readonly ILogger<FirebaseItemPhotoRepository> _logger;
        private readonly IMediaService _mediaService;
        private readonly IFirebaseTransactionHelper _transactionHelper;
        private const string PhotosPath = "photos";
        private const int MaxPhotosPerItem = 10;

        public FirebaseItemPhotoRepository(
            IAppCoreDataStore dataStore,
            IItemRepository itemRepository,
            IMediaService mediaService,
            ILogger<FirebaseItemPhotoRepository> logger,
            IFirebaseTransactionHelper transactionHelper)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _transactionHelper = transactionHelper ?? throw new ArgumentNullException(nameof(transactionHelper));
        }

        /// <summary>
        /// Get all photos with pagination support
        /// </summary>
        public async Task<List<ItemPhoto>> GetAllAsync()
        {
            try
            {
                var firebasePhotos = await _dataStore.GetCollectionAsync<FirebaseItemPhoto>(PhotosPath);
                return firebasePhotos
                    .Select(fp => fp?.ToItemPhoto())
                    .Where(p => p != null)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all photos");
                return new List<ItemPhoto>();
            }
        }

        /// <summary>
        /// Get all photos for an item
        /// </summary>
        public async Task<List<ItemPhoto>> GetByItemIdAsync(string itemId)
        {
            try
            {
                var photoEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"item_photos/{itemId}");

                if (photoEntries == null || !photoEntries.Any())
                    return new List<ItemPhoto>();

                var photoTasks = photoEntries.Select(async entry =>
                {
                    if (entry.FirstOrDefault().Key != null)
                    {
                        string photoId = entry.FirstOrDefault().Key;
                        return await GetByIdAsync(photoId);
                    }
                    return null;
                }).ToList();

                var photos = await Task.WhenAll(photoTasks);

                return photos
                    .Where(p => p != null)
                    .OrderBy(p => p.DisplayOrder)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting photos for item {itemId}");
                return new List<ItemPhoto>();
            }
        }

        /// <summary>
        /// Get a photo by its ID
        /// </summary>
        public async Task<ItemPhoto> GetByIdAsync(string photoId)
        {
            try
            {
                var firebasePhoto = await _dataStore.GetDocumentAsync<FirebaseItemPhoto>($"{PhotosPath}/{photoId}");
                return firebasePhoto?.ToItemPhoto();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting photo with ID {photoId}");
                return null;
            }
        }

        /// <summary>
        /// Set a photo as the primary photo for an item with atomic operations
        /// </summary>
        public async Task<PhotoOperationResult> SetPrimaryPhotoAsync(string userId, string photoId)
        {
            try
            {
                // Get the photo and verify permissions
                var photo = await GetByIdAsync(photoId);
                if (photo == null)
                {
                    return PhotoOperationResult.Failure($"Photo with ID {photoId} not found");
                }

                // Ensure photo.ItemId is valid
                if (string.IsNullOrEmpty(photo.ItemId))
                {
                    return PhotoOperationResult.Failure("Photo's ItemId is null or empty");
                }

                var item = await _itemRepository.GetByIdAsync(photo.ItemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {photo.ItemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to modify this photo");
                }

                // If this photo is already primary, no action needed
                if (photo.IsPrimaryPhoto)
                {
                    return PhotoOperationResult.Success(photo);
                }

                // Use transaction to ensure atomic update
                var result = await _transactionHelper.ExecuteAtomicOperationAsync(async transaction =>
                {
                    // Get all photos for the item
                    var allPhotos = await GetByItemIdAsync(photo.ItemId);

                    // Find and update the current primary
                    var currentPrimary = allPhotos.FirstOrDefault(p => p.IsPrimaryPhoto);
                    if (currentPrimary != null)
                    {
                        currentPrimary.IsPrimaryPhoto = false;
                        currentPrimary.Version++;
                        await transaction.UpdateEntityAsync($"{PhotosPath}/{currentPrimary.Id}",
                            FirebaseItemPhoto.FromItemPhoto(currentPrimary));
                    }

                    // Update the new primary
                    photo.IsPrimaryPhoto = true;
                    photo.Version++;
                    await transaction.UpdateEntityAsync($"{PhotosPath}/{photo.Id}",
                        FirebaseItemPhoto.FromItemPhoto(photo));

                    // Update the item's primary photo URL
                    item.PhotoUrl = photo.PhotoUrl;
                    item.Version++;
                    await transaction.UpdateEntityAsync($"items/{item.Id}",
                        FirebaseItem.FromItem(item));

                    return photo;
                });

                return result.Success
                    ? PhotoOperationResult.Success(result.Data)
                    : PhotoOperationResult.Failure(result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting primary photo {photoId}");
                return PhotoOperationResult.Failure($"Error setting primary photo: {ex.Message}");
            }
        }

        /// <summary>
        /// Reorder photos for an item with atomic operations
        /// </summary>
        public async Task<PhotoOperationResult> ReorderPhotosAsync(string userId, string itemId, List<string> photoIds)
        {
            try
            {
                if (photoIds == null || !photoIds.Any())
                {
                    return PhotoOperationResult.Failure("No photo IDs provided");
                }

                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to reorder photos for this item");
                }

                // Get all photos for this item
                var allPhotos = await GetByItemIdAsync(itemId);

                // Check if all provided photoIds belong to this item
                var itemPhotoIds = allPhotos.Select(p => p.Id).ToHashSet();
                if (photoIds.Any(pid => !itemPhotoIds.Contains(pid)))
                {
                    return PhotoOperationResult.Failure("One or more photo IDs do not belong to this item");
                }

                if (photoIds.Count != allPhotos.Count)
                {
                    return PhotoOperationResult.Failure("The reordering must include all photos of the item");
                }

                var result = await _transactionHelper.ExecuteAtomicOperationAsync(async transaction =>
                {
                    for (int i = 0; i < photoIds.Count; i++)
                    {
                        string pid = photoIds[i];
                        var photo = allPhotos.First(p => p.Id == pid);

                        photo.DisplayOrder = i.ToString();
                        photo.Version++;

                        await transaction.UpdateEntityAsync($"{PhotosPath}/{pid}", FirebaseItemPhoto.FromItemPhoto(photo));
                        await transaction.UpdateEntityAsync($"item_photos/{itemId}/{pid}/displayOrder", i);
                    }
                    return true;
                });

                return result.Success
                    ? PhotoOperationResult.Success()
                    : PhotoOperationResult.Failure(result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reordering photos for item {itemId}");
                return PhotoOperationResult.Failure($"Error reordering photos: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a photo to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoAsync(string userId, string itemId, FileResult photoFile)
        {
            try
            {
                // Validate inputs
                if (photoFile == null)
                {
                    return PhotoOperationResult.Failure("No photo file provided");
                }

                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to add photos to this item");
                }

                // Check photo limit
                var existingPhotos = await GetByItemIdAsync(itemId);
                int maxPhotos = item.MaxPhotos > 0 ? item.MaxPhotos : MaxPhotosPerItem;

                if (existingPhotos.Count >= maxPhotos)
                {
                    return PhotoOperationResult.Failure($"Maximum photo limit of {maxPhotos} reached for this item");
                }

                // Process and upload the photo file
                using (var stream = await photoFile.OpenReadAsync())
                {
                    // Upload to storage
                    var uploadResult = await _mediaService.UploadImageAsync(stream, photoFile.FileName);
                    string photoUrl = uploadResult.downloadUrl;
                    string storagePath = uploadResult.storagePath;

                    if (string.IsNullOrEmpty(photoUrl))
                    {
                        return PhotoOperationResult.Failure("Failed to upload photo to storage");
                    }

                    // Create domain model photo
                    var photo = new ItemPhoto
                    {
                        Id = await GetNextPhotoIdAsync(),
                        ItemId = itemId,
                        PhotoUrl = photoUrl,
                        StoragePath = storagePath,
                        DisplayOrder = existingPhotos.Count.ToString(),
                        IsPrimaryPhoto = !existingPhotos.Any(p => p.IsPrimaryPhoto),
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    };

                    // Convert to Firebase model
                    var firebasePhoto = FirebaseItemPhoto.FromItemPhoto(photo);

                    // Create multi-path update including indexes
                    var updates = new Dictionary<string, object>
                    {
                        [$"{PhotosPath}/{firebasePhoto.Id}"] = firebasePhoto.ToFirebaseObject(),
                        [$"item_photos/{itemId}/{firebasePhoto.Id}/displayOrder"] = photo.DisplayOrder,
                        [$"item_photos/{itemId}/{firebasePhoto.Id}/isPrimary"] = photo.IsPrimaryPhoto
                    };

                    // If this is the primary photo, update the item
                    if (photo.IsPrimaryPhoto)
                    {
                        updates[$"items/{itemId}/photoUrl"] = photo.PhotoUrl;
                    }

                    // Execute as atomic update
                    await _dataStore.BatchUpdateAsync(updates);

                    return PhotoOperationResult.Success(photo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding photo to item {itemId}");
                return PhotoOperationResult.Failure($"Error adding photo: {ex.Message}");
            }
        }

        /// <summary>
        /// Add multiple photos with atomic operations
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotosAsync(string userId, string itemId, ICollection<FileResult> photoFiles)
        {
            try
            {
                // Validate inputs and ownership
                if (photoFiles == null || !photoFiles.Any())
                {
                    return PhotoOperationResult.Failure("No photo files provided");
                }

                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to add photos to this item");
                }

                // Check photo limit
                var existingPhotos = await GetByItemIdAsync(itemId);
                int maxPhotos = item.MaxPhotos > 0 ? item.MaxPhotos : MaxPhotosPerItem;

                if (existingPhotos.Count + photoFiles.Count > maxPhotos)
                {
                    return PhotoOperationResult.Failure($"Adding these photos would exceed the maximum limit of {maxPhotos}");
                }

                // Process each photo and prepare for bulk operation
                var uploadedPhotos = new List<ItemPhoto>();
                var updates = new Dictionary<string, object>();

                int startOrder = existingPhotos.Count;
                bool needsPrimary = !existingPhotos.Any(p => p.IsPrimaryPhoto);

                foreach (var photoFile in photoFiles)
                {
                    try
                    {
                        using (var stream = await photoFile.OpenReadAsync())
                        {
                            // Upload to storage
                            var uploadResult = await _mediaService.UploadImageAsync(stream, photoFile.FileName);
                            if (string.IsNullOrEmpty(uploadResult.downloadUrl))
                            {
                                continue; // Skip failed uploads
                            }

                            // Create domain model photo
                            var photo = new ItemPhoto
                            {
                                Id = await GetNextPhotoIdAsync(),
                                ItemId = itemId,
                                PhotoUrl = uploadResult.downloadUrl,
                                StoragePath = uploadResult.storagePath,
                                DisplayOrder = startOrder++.ToString(),
                                IsPrimaryPhoto = needsPrimary && uploadedPhotos.Count == 0, // First photo is primary if needed
                                UploadedAt = DateTime.UtcNow,
                                Version = 1
                            };

                            // Convert to Firebase model
                            var firebasePhoto = FirebaseItemPhoto.FromItemPhoto(photo);

                            // Add to updates dictionary
                            updates[$"{PhotosPath}/{firebasePhoto.Id}"] = firebasePhoto.ToFirebaseObject();
                            updates[$"item_photos/{itemId}/{firebasePhoto.Id}/displayOrder"] = photo.DisplayOrder;
                            updates[$"item_photos/{itemId}/{firebasePhoto.Id}/isPrimary"] = photo.IsPrimaryPhoto;

                            uploadedPhotos.Add(photo);

                            if (photo.IsPrimaryPhoto)
                            {
                                needsPrimary = false;
                                updates[$"items/{itemId}/photoUrl"] = photo.PhotoUrl;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing photo file {photoFile.FileName}");
                        // Continue with other photos
                    }
                }

                // Execute bulk operation atomically
                if (updates.Any())
                {
                    await _dataStore.BatchUpdateAsync(updates);
                    return PhotoOperationResult.Success(uploadedPhotos);
                }

                return PhotoOperationResult.Failure("Failed to upload any photos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding bulk photos to item {itemId}");
                return PhotoOperationResult.Failure($"Error adding photos: {ex.Message}");
            }
        }

        /// <summary>
        /// Add a photo URL to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoUrlAsync(string userId, string itemId, string photoUrl)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(photoUrl))
                {
                    return PhotoOperationResult.Failure("No photo URL provided");
                }

                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to add photos to this item");
                }

                // Check photo limit
                var existingPhotos = await GetByItemIdAsync(itemId);
                int maxPhotos = item.MaxPhotos > 0 ? item.MaxPhotos : MaxPhotosPerItem;

                if (existingPhotos.Count >= maxPhotos)
                {
                    return PhotoOperationResult.Failure($"Maximum photo limit of {maxPhotos} reached for this item");
                }

                // Create domain model photo
                var photo = new ItemPhoto
                {
                    Id = await GetNextPhotoIdAsync(),
                    ItemId = itemId,
                    PhotoUrl = photoUrl,
                    DisplayOrder = existingPhotos.Count.ToString(),
                    IsPrimaryPhoto = !existingPhotos.Any(p => p.IsPrimaryPhoto),
                    UploadedAt = DateTime.UtcNow,
                    Version = 1
                };

                // Convert to Firebase model
                var firebasePhoto = FirebaseItemPhoto.FromItemPhoto(photo);

                // Create multi-path update
                var updates = new Dictionary<string, object>
                {
                    [$"{PhotosPath}/{firebasePhoto.Id}"] = firebasePhoto.ToFirebaseObject(),
                    [$"item_photos/{itemId}/{firebasePhoto.Id}/displayOrder"] = photo.DisplayOrder.ToString(),
                    [$"item_photos/{itemId}/{firebasePhoto.Id}/isPrimary"] = photo.IsPrimaryPhoto
                };

                // If this is the primary photo, update the item
                if (photo.IsPrimaryPhoto)
                {
                    updates[$"items/{itemId}/photoUrl"] = photo.PhotoUrl;
                }

                // Execute as atomic update
                await _dataStore.BatchUpdateAsync(updates);

                return PhotoOperationResult.Success(photo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding photo URL to item {itemId}");
                return PhotoOperationResult.Failure($"Error adding photo URL: {ex.Message}");
            }
        }

        /// <summary>
        /// Add multiple photo URLs to an item
        /// </summary>
        public async Task<PhotoOperationResult> AddPhotoUrlsAsync(string userId, string itemId, IEnumerable<string> photoUrls)
        {
            try
            {
                // Validate inputs
                if (photoUrls == null || !photoUrls.Any())
                {
                    return PhotoOperationResult.Failure("No photo URLs provided");
                }

                // Verify item ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to add photos to this item");
                }

                // Check photo limit
                var existingPhotos = await GetByItemIdAsync(itemId);
                int maxPhotos = item.MaxPhotos > 0 ? item.MaxPhotos : MaxPhotosPerItem;

                if (existingPhotos.Count + photoUrls.Count() > maxPhotos)
                {
                    return PhotoOperationResult.Failure($"Adding these photos would exceed the maximum limit of {maxPhotos}");
                }

                // Process each photo URL
                var addedPhotos = new List<ItemPhoto>();
                int displayOrder = existingPhotos.Count;
                bool needsPrimary = !existingPhotos.Any(p => p.IsPrimaryPhoto);
                var updates = new Dictionary<string, object>();

                foreach (var url in photoUrls)
                {
                    if (string.IsNullOrEmpty(url)) continue;

                    // Create domain model photo
                    var photo = new ItemPhoto
                    {
                        Id = await GetNextPhotoIdAsync(),
                        ItemId = itemId,
                        PhotoUrl = url,
                        DisplayOrder = displayOrder++.ToString(),
                        IsPrimaryPhoto = needsPrimary && addedPhotos.Count == 0, // First photo is primary if needed
                        UploadedAt = DateTime.UtcNow,
                        Version = 1
                    };

                    // Convert to Firebase model
                    var firebasePhoto = FirebaseItemPhoto.FromItemPhoto(photo);

                    // Add photo to batch updates
                    updates[$"{PhotosPath}/{firebasePhoto.Id}"] = firebasePhoto.ToFirebaseObject();
                    updates[$"item_photos/{itemId}/{firebasePhoto.Id}/displayOrder"] = photo.DisplayOrder;
                    updates[$"item_photos/{itemId}/{firebasePhoto.Id}/isPrimary"] = photo.IsPrimaryPhoto;

                    addedPhotos.Add(photo);

                    if (photo.IsPrimaryPhoto)
                    {
                        needsPrimary = false;
                        updates[$"items/{itemId}/photoUrl"] = photo.PhotoUrl;
                    }
                }

                if (addedPhotos.Count == 0)
                {
                    return PhotoOperationResult.Failure("Failed to add any photos");
                }

                // Execute all updates as a single batch
                await _dataStore.BatchUpdateAsync(updates);

                return PhotoOperationResult.Success(addedPhotos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding photo URLs to item {itemId}");
                return PhotoOperationResult.Failure($"Error adding photo URLs: {ex.Message}");
            }
        }

        /// <summary>
        /// Delete multiple photos
        /// </summary>
        public async Task<PhotoOperationResult> DeletePhotosAsync(string userId, string itemId, IEnumerable<string> photoIds)
        {
            try
            {
                if (photoIds == null || !photoIds.Any())
                {
                    return PhotoOperationResult.Failure("No photo IDs provided");
                }

                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null)
                {
                    return PhotoOperationResult.Failure($"Item with ID {itemId} not found");
                }

                if (item.PostedByUserId != userId)
                {
                    return PhotoOperationResult.Failure("You do not have permission to delete photos for this item");
                }

                var allPhotos = await GetByItemIdAsync(itemId);
                var photosToDelete = allPhotos
                    .Where(p => photoIds.Contains(p.Id))
                    .ToList();

                if (!photosToDelete.Any())
                {
                    return PhotoOperationResult.Failure("None of the specified photos were found");
                }

                bool deletingPrimary = photosToDelete.Any(p => p.IsPrimaryPhoto);
                var updates = new Dictionary<string, object>();

                foreach (var photo in photosToDelete)
                {
                    if (!string.IsNullOrEmpty(photo.StoragePath))
                    {
                        try
                        {
                            await _mediaService.DeleteImageAsync(photo.PhotoUrl);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Error deleting photo from storage at path: {photo.StoragePath}");
                        }
                    }
                    updates[$"{PhotosPath}/{photo.Id}"] = null;
                    updates[$"item_photos/{itemId}/{photo.Id}"] = null;
                }

                if (deletingPrimary)
                {
                    var remainingPhotos = allPhotos.Where(p => !photoIds.Contains(p.Id)).ToList();
                    if (remainingPhotos.Any())
                    {
                        var newPrimary = remainingPhotos.OrderBy(p => p.DisplayOrder).First();
                        newPrimary.IsPrimaryPhoto = true;
                        var firebaseNewPrimary = FirebaseItemPhoto.FromItemPhoto(newPrimary);
                        updates[$"{PhotosPath}/{newPrimary.Id}"] = firebaseNewPrimary.ToFirebaseObject();
                        updates[$"item_photos/{newPrimary.ItemId}/{newPrimary.Id}/isPrimary"] = true;
                        updates[$"items/{itemId}/photoUrl"] = newPrimary.PhotoUrl;
                    }
                    else
                    {
                        updates[$"items/{itemId}/photoUrl"] = null;
                    }
                }
                await _dataStore.BatchUpdateAsync(updates);
                return PhotoOperationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting photos for item {itemId}");
                return PhotoOperationResult.Failure($"Error deleting photos: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if a user has permission to manage a photo
        /// </summary>
        public async Task<bool> CanUserManagePhotoAsync(string userId, string photoId)
        {
            try
            {
                // Get the photo
                var photo = await GetByIdAsync(photoId);
                if (photo == null || string.IsNullOrEmpty(photo.ItemId))
                {
                    return false;
                }

                // Get the item
                var item = await _itemRepository.GetByIdAsync(photo.ItemId);
                if (item == null)
                {
                    return false;
                }

                return item.PostedByUserId == userId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if user {userId} can manage photo {photoId}");
                return false;
            }
        }

        /// <summary>
        /// Get the next available photo ID
        /// </summary>
        public async Task<string> GetNextPhotoIdAsync()
        {
            try
            {
                var photos = await GetAllAsync();
                if (!photos.Any())
                    return "1";

                // Parse the IDs as integers where possible
                var maxId = photos
                    .Select(p => int.TryParse(p.Id, out int id) ? id : 0)
                    .Max();

                return (maxId + 1).ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting next photo ID");

                // Fallback to a timestamp-based ID to ensure uniqueness
                return DateTime.UtcNow.Ticks.ToString().Substring(0, 9);
            }
        }

        /// <summary>
        /// Update photo metadata
        /// </summary>
        public async Task<bool> UpdatePhotoAsync(ItemPhoto photo)
        {
            try
            {
                if (photo == null || string.IsNullOrEmpty(photo.Id))
                {
                    return false;
                }

                // Convert to Firebase model
                var firebasePhoto = FirebaseItemPhoto.FromItemPhoto(photo);

                // Create multi-path update
                var updates = new Dictionary<string, object>
                {
                    [$"{PhotosPath}/{firebasePhoto.Id}"] = firebasePhoto.ToFirebaseObject(),
                    [$"item_photos/{photo.ItemId}/{firebasePhoto.Id}/displayOrder"] = photo.DisplayOrder,
                    [$"item_photos/{photo.ItemId}/{firebasePhoto.Id}/isPrimary"] = photo.IsPrimaryPhoto
                };

                await _dataStore.BatchUpdateAsync(updates);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating photo {photo?.Id}");
                return false;
            }
        }



        /// <summary>
        /// Delete a single photo
        /// </summary>
        public async Task<PhotoOperationResult> DeletePhotoAsync(string userId, string photoId)
        {
            try
            {
                if (string.IsNullOrEmpty(photoId))
                {
                    return PhotoOperationResult.Failure("No photo ID provided");
                }

                // Get the photo to find its item ID
                var photo = await GetByIdAsync(photoId);
                if (photo == null)
                {
                    return PhotoOperationResult.Failure($"Photo with ID {photoId} not found");
                }

                // Use the existing multiple photo deletion method
                return await DeletePhotosAsync(userId, photo.ItemId, new[] { photoId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting photo {photoId}");
                return PhotoOperationResult.Failure($"Error deleting photo: {ex.Message}");
            }
        }


    }
}