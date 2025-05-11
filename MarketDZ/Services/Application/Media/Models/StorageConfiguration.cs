using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Media.Models
{
    public class StorageConfiguration
    {
        public string? FirebaseStorageBucket { get; set; }
        public int MaxImageWidth { get; set; } = 1920;
        public int MaxImageHeight { get; set; } = 1080;
        public long MaxFileSize { get; set; } = 10 * 1024 * 1024; // 10MB
        public int ImageQuality { get; set; } = 80;
        public int ThumbnailSize { get; set; } = 150;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(FirebaseStorageBucket))
            {
                throw new ArgumentException(
                    "Firebase Storage Bucket configuration is missing. " +
                    "Please configure in appsettings.json under 'StorageSettings' or 'Firebase' section. " +
                    "Ensure the bucket name follows the format 'project-id.appspot.com'.",
                    nameof(FirebaseStorageBucket));
            }

            // Additional validations
            if (MaxImageWidth <= 0 || MaxImageHeight <= 0)
            {
                throw new ArgumentException("Image dimensions must be positive.");
            }

            if (MaxFileSize <= 0)
            {
                throw new ArgumentException("Max file size must be positive.");
            }

            if (ImageQuality < 1 || ImageQuality > 100)
            {
                throw new ArgumentException("Image quality must be between 1 and 100.");
            }

            if (ThumbnailSize <= 0)
            {
                throw new ArgumentException("Thumbnail size must be positive.");
            }
        }
    }
}