using System;

namespace MarketDZ.Models.Helpers
{
    /// <summary>
    /// Represents metadata for an image stored in Firebase Storage
    /// </summary>
    public class ImageMetadata
    {
        /// <summary>
        /// The unique identifier for the metadata record
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The unique filename assigned when uploading
        /// </summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>
        /// The original filename before upload
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        /// <summary>
        /// The MIME content type of the image
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// When the image was uploaded
        /// </summary>
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The path to the image in Firebase Storage
        /// </summary>
        public string StoragePath { get; set; } = string.Empty;

        /// <summary>
        /// The public download URL for the image
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;

        /// <summary>
        /// The file size in bytes, if available
        /// </summary>
        public long? SizeBytes { get; set; }

        /// <summary>
        /// The user ID of the uploader, if applicable
        /// </summary>
        public string?  UploadedByUserId { get; set; }

        /// <summary>
        /// Optional item ID if this image is associated with a specific item
        /// </summary>
        public string? ItemId { get; set; }
    }
}