using MarketDZ.Models;
using Microsoft.Maui.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemPhotoService
    {
        /// <summary> Reorders photos for an item based on a list of photo IDs. </summary> //
        Task<bool> ReorderPhotosAsync(string itemId, List<string> photoIds);

        /// <summary> Adds a collection of photos (from FileResult) to an item. </summary> //
        Task<bool> AddPhotosToItemAsync(string itemId, string userId, ICollection<FileResult> photos);

        /// <summary> Adds a single photo (from FileResult) to an item. </summary> //
        Task<ItemPhoto?> AddItemPhotoAsync(string userId, string itemId, FileResult photoFile);

        /// <summary> Adds a photo to an item using a URL (legacy). </summary> //
        Task<bool> AddItemPhotoAsync(string userId, string itemId, string photoUrl);

        /// <summary> Removes a specific photo by its ID. </summary> //
        Task<bool> RemoveItemPhotoAsync(string userId, string photoId);

        /// <summary> Updates photo metadata based on URLs (potentially linked to legacy URL method). </summary> //
        Task<bool> UpdateItemPhotoMetadataAsync(string itemId, string userId, List<string> photoUrls);

        /// <summary> Retrieves all photos associated with a specific item. </summary> //
        Task<List<ItemPhoto>> GetItemPhotosAsync(string itemId);

        /// <summary> Sets a specific photo as the primary/cover photo for an item. </summary> //
        Task<bool> SetPrimaryPhotoAsync(string userId, string photoId);

        /// <summary> Reorders photos for an item (appears functionally identical to ReorderPhotosAsync). </summary> //
        Task<bool> ReorderItemPhotosAsync(string userId, string itemId, List<string> photoIds);

        /// <summary> Retrieves all photos across all items. </summary> //
        Task<List<ItemPhoto>> GetAllItemPhotosAsync();

        /// <summary> Adds multiple photos (from FileResult enumeration) to an item. </summary> //
        Task<List<ItemPhoto>> AddItemPhotosAsync(string userId, string itemId, IEnumerable<FileResult> photoFiles);
        Task UpdateItemPhotoMetadataAsync(string itemId, string userId, ICollection<string> photoUrls);
        Task DeletePhotosAsync(string postedByUserId, string id, List<string> list);
    }
}