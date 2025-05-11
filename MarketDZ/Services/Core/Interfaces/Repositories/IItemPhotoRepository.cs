using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Core.Interfaces.Repositories
{
    /// <summary>
    /// Repository interface for item photo operations
    /// </summary>
    public interface IItemPhotoRepository
    {
        /// <summary>
        /// Get a photo by its ID
        /// </summary>
        /// <param name="photoId">Photo ID</param>
        /// <returns>Item photo or null if not found</returns>
        Task<ItemPhoto> GetByIdAsync(string photoId);

        /// <summary>
        /// Get all photos for an item
        /// </summary>
        /// <param name="itemId">Item ID</param>
        /// <returns>List of photos for the item</returns>
        Task<List<ItemPhoto>> GetByItemIdAsync(string itemId);

        /// <summary>
        /// Get all photos
        /// </summary>
        /// <returns>List of all photos</returns>
        Task<List<ItemPhoto>> GetAllAsync();

        /// <summary>
        /// Add a photo to an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoFile">Photo file</param>
        /// <returns>Result with photo information or error</returns>
        Task<PhotoOperationResult> AddPhotoAsync(string userId, string itemId, FileResult photoFile);

        /// <summary>
        /// Add multiple photos to an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoFiles">Collection of photo files</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> AddPhotosAsync(string userId, string itemId, ICollection<FileResult> photoFiles);

        /// <summary>
        /// Add a photo URL to an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoUrl">Photo URL</param>
        /// <returns>Result with photo information or error</returns>
        Task<PhotoOperationResult> AddPhotoUrlAsync(string userId, string itemId, string photoUrl);

        /// <summary>
        /// Add multiple photo URLs to an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoUrls">Collection of photo URLs</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> AddPhotoUrlsAsync(string userId, string itemId, IEnumerable<string> photoUrls);

        /// <summary>
        /// Delete a photo
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="photoId">Photo ID</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> DeletePhotoAsync(string userId, string photoId);

        /// <summary>
        /// Delete multiple photos
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoIds">Collection of photo IDs</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> DeletePhotosAsync(string userId, string itemId, IEnumerable<string> photoIds);

        /// <summary>
        /// Set a photo as the primary photo for an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="photoId">Photo ID</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> SetPrimaryPhotoAsync(string userId, string photoId);

        /// <summary>
        /// Reorder photos for an item
        /// </summary>
        /// <param name="userId">User ID (owner of the item)</param>
        /// <param name="itemId">Item ID</param>
        /// <param name="photoIds">Ordered list of photo IDs</param>
        /// <returns>Result with operation status</returns>
        Task<PhotoOperationResult> ReorderPhotosAsync(string userId, string itemId, List<string> photoIds);

        /// <summary>
        /// Update photo metadata
        /// </summary>
        /// <param name="photo">Updated photo</param>
        /// <returns>True if update was successful</returns>
        Task<bool> UpdatePhotoAsync(ItemPhoto photo);

        /// <summary>
        /// Get the next available photo ID
        /// </summary>
        /// <returns>Next available ID</returns>
        Task<string> GetNextPhotoIdAsync();

        /// <summary>
        /// Check if a user has permission to manage a photo
        /// </summary>
        /// <param name="userId">User ID</param>
        /// <param name="photoId">Photo ID</param>
        /// <returns>True if user has permission</returns>
        Task<bool> CanUserManagePhotoAsync(string userId, string photoId);
    }

    /// <summary>
    /// Result of a photo operation
    /// </summary>
    public class PhotoOperationResult
    {
        /// <summary>
        /// Indicates if the operation was successful
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Error message if the operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Photo created by the operation (if applicable)
        /// </summary>
        public ItemPhoto? Photo { get; set; }

        /// <summary>
        /// Collection of photos created by the operation (if applicable)
        /// </summary>
        public List<ItemPhoto> Photos { get; set; }

        /// <summary>
        /// Creates a successful result with a single photo
        /// </summary>
        public static PhotoOperationResult Success(ItemPhoto photo = null!)
        {
            return new PhotoOperationResult
            {
                IsValid = true,
                Photo = photo
            };
        }

        /// <summary>
        /// Creates a successful result with multiple photos
        /// </summary>
        public static PhotoOperationResult Success(List<ItemPhoto> photos)
        {
            return new PhotoOperationResult
            {
                IsValid = true,
                Photos = photos
            };
        }

        /// <summary>
        /// Creates a failed result with an error message
        /// </summary>
        public static PhotoOperationResult Failure(string errorMessage)
        {
            return new PhotoOperationResult
            {
                IsValid = false,
                ErrorMessage = errorMessage
            };
        }
    }
}