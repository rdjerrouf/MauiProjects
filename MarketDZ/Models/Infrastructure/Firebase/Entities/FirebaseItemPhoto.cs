using System;
using System.Collections.Generic;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using Newtonsoft.Json;
namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Firebase-specific implementation of the ItemPhoto model
    /// </summary>
    public class FirebaseItemPhoto : FirebaseEntity, IDomainModelConverter<ItemPhoto>, IVersionedEntity
    {
        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; }

        /// <summary>
        /// The item this photo belongs to
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// URL to the photo
        /// </summary>
        public string PhotoUrl { get; set; } = string.Empty;

        /// <summary>
        /// Storage path for the photo
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// Whether this is the primary photo for the item
        /// </summary>
        public bool IsPrimaryPhoto { get; set; }

        /// <summary>
        /// Display order for this photo
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Optional caption for the photo
        /// </summary>
        public string Caption { get; set; } = string.Empty;

        /// <summary>
        /// Creates a FirebaseItemPhoto from an ItemPhoto domain model
        /// </summary>
        public static FirebaseItemPhoto FromItemPhoto(ItemPhoto photo, string id = "")
        {
            var result = new FirebaseItemPhoto
            {
                Id = string.IsNullOrEmpty(id) ? GenerateId() : id,
                ItemId = photo.ItemId.ToString(),
                PhotoUrl = photo.PhotoUrl,
                StoragePath = photo.StoragePath ?? string.Empty,
                IsPrimaryPhoto = photo.IsPrimaryPhoto,
                DisplayOrder = int.TryParse(photo.DisplayOrder, out var displayOrder) ? displayOrder : 0, // Safely parse string to int
                Caption = photo.Caption ?? string.Empty,
                CreatedTimestamp = photo.UploadedAt.ToUniversalTime().Ticks / 10000, // Convert to milliseconds
                Version = photo.Version,
                LastModified = photo.LastModified
            };

            return result;
        }

        /// <summary>
        /// Converts back to an ItemPhoto domain model
        /// </summary>
        public ItemPhoto ToItemPhoto()
        {
            var photo = new ItemPhoto
            {
                PhotoUrl = this.PhotoUrl,
                StoragePath = this.StoragePath,
                IsPrimaryPhoto = this.IsPrimaryPhoto,
                DisplayOrder = this.DisplayOrder.ToString(), // Convert int to string
                Caption = this.Caption,
                UploadedAt = this.CreatedAt,
                Version = this.Version,
                LastModified = this.LastModified
            };

            // Convert string IDs to integers for compatibility
            photo.Id = this.Id ?? string.Empty;
            photo.ItemId = this.ItemId ?? string.Empty;
            return photo;
        }
        /// <summary>
        /// Creates index entries for this photo
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var updates = new Dictionary<string, object>();

            // Index photos by item
            updates[$"item_photos/{ItemId}/{Id}"] = new Dictionary<string, object>
            {
                ["displayOrder"] = DisplayOrder,
                ["isPrimary"] = IsPrimaryPhoto,
                ["photoUrl"] = PhotoUrl
            };

            // If this is the primary photo, update the item's primary photo
            if (IsPrimaryPhoto)
            {
                updates[$"items/{ItemId}/photoUrl"] = PhotoUrl;
            }

            return updates;
        }

        /// <summary>
        /// Creates removal entries for this photo (for deleting)
        /// </summary>
        public Dictionary<string, object> CreateRemovalEntries()
        {
            var updates = new Dictionary<string, object>();

            // Remove from item_photos index
            updates[$"item_photos/{ItemId}/{Id}"] = null;

            // The photo itself
            updates[$"photos/{Id}"] = null;

            return updates;
        }

        /// <summary>
        /// Converts to a Firebase-compatible dictionary
        /// </summary>
        public override Dictionary<string, object> ToFirebaseObject()
        {
            var result = base.ToFirebaseObject();

            result["itemId"] = ItemId;
            result["photoUrl"] = PhotoUrl;
            result["storagePath"] = StoragePath;
            result["isPrimaryPhoto"] = IsPrimaryPhoto;
            result["displayOrder"] = DisplayOrder;

            if (!string.IsNullOrEmpty(Caption))
                result["caption"] = Caption;

            return result;
        }

        public ItemPhoto ToDomainModel()
        {
            throw new NotImplementedException();
        }
    }
}