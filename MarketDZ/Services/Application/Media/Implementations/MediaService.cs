using Firebase.Storage;
using MarketDZ.Services.Media; // Contains IMediaService, StorageConfiguration
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Storage; // For FileResult
using SkiaSharp; // Use SkiaSharp for image processing
using System;
using System.IO;
using System.Linq;
using System.Net; // For WebUtility
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Media.Implementations
{
    public class MediaService : IMediaService
    {
        private readonly ILogger<MediaService> _logger;
        private readonly StorageConfiguration _storageConfig;
        private readonly FirebaseStorage _firebaseStorage; // Firebase Storage client

        public MediaService(
            ILogger<MediaService> logger,
            IOptions<StorageConfiguration> storageOptions)
        {
            // Null checks and argument validation
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Comprehensive null and configuration validation
            if (storageOptions?.Value == null)
            {
                _logger.LogCritical("StorageConfiguration is null or not configured");
                throw new InvalidOperationException(
                    "Storage configuration is missing. " +
                    "Ensure appsettings.json is correctly configured with Firebase Storage Bucket.");
            }

            // Get the actual configuration
            _storageConfig = storageOptions.Value;

            // Detailed logging of configuration
            _logger.LogInformation("Storage Configuration Details:");
            _logger.LogInformation($"Firebase Storage Bucket: {_storageConfig.FirebaseStorageBucket}");
            _logger.LogInformation($"Max Image Width: {_storageConfig.MaxImageWidth}");
            _logger.LogInformation($"Max Image Height: {_storageConfig.MaxImageHeight}");
            _logger.LogInformation($"Max File Size: {_storageConfig.MaxFileSize} bytes");
            _logger.LogInformation($"Image Quality: {_storageConfig.ImageQuality}");
            _logger.LogInformation($"Thumbnail Size: {_storageConfig.ThumbnailSize}");

            // Validate configuration
            try
            {
                _storageConfig.Validate();
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex, "Storage configuration validation failed");
                throw; // Re-throw to prevent service initialization with invalid config
            }

            // Initialize FirebaseStorage client with comprehensive error handling
            try
            {
                _firebaseStorage = new FirebaseStorage(
                    _storageConfig.FirebaseStorageBucket,
                    new FirebaseStorageOptions
                    {
                        // Optional: Add retry and timeout configurations
                        AuthTokenAsyncFactory = () => Task.FromResult(string.Empty),
                        HttpClientTimeout = TimeSpan.FromSeconds(30)
                    }
                );

                _logger.LogInformation("Firebase Storage client initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase Storage client");
                throw new InvalidOperationException("Could not create Firebase Storage client", ex);
            }
        }
        /// <summary>
        /// Validates the StorageConfiguration to ensure all critical settings are correctly configured
        /// </summary>
        private void ValidateStorageConfiguration(StorageConfiguration config)
        {
            // Validate Firebase Storage Bucket
            if (string.IsNullOrWhiteSpace(config.FirebaseStorageBucket))
            {
                _logger.LogCritical("Firebase Storage Bucket is not configured");
                throw new ArgumentException("Firebase Storage Bucket must be configured", nameof(config.FirebaseStorageBucket));
            }

            // Validate image dimensions
            if (config.MaxImageWidth <= 0 || config.MaxImageHeight <= 0)
            {
                _logger.LogWarning("Invalid image dimension limits: Width={Width}, Height={Height}",
                    config.MaxImageWidth, config.MaxImageHeight);
                config.MaxImageWidth = 1920;  // Set default
                config.MaxImageHeight = 1080; // Set default
            }

            // Validate file size
            if (config.MaxFileSize <= 0)
            {
                _logger.LogWarning("Invalid max file size: {Size}. Setting default.", config.MaxFileSize);
                config.MaxFileSize = 10 * 1024 * 1024; // 10MB default
            }

            // Validate image quality
            if (config.ImageQuality < 1 || config.ImageQuality > 100)
            {
                _logger.LogWarning("Invalid image quality: {Quality}. Setting default.", config.ImageQuality);
                config.ImageQuality = 80; // Default quality
            }

            // Validate thumbnail size
            if (config.ThumbnailSize <= 0)
            {
                _logger.LogWarning("Invalid thumbnail size: {Size}. Setting default.", config.ThumbnailSize);
                config.ThumbnailSize = 150; // Default thumbnail size
            }
        }

        // Rest of the existing MediaService implementation remains the same...
        // (Your existing methods like ProcessImageAsync, GenerateThumbnailAsync, UploadImageAsync, etc.)

        // Optional: Add a method to log configuration for debugging
        public void LogMediaServiceConfiguration()
        {
            _logger.LogInformation("Media Service Configuration:");
            _logger.LogInformation("Firebase Storage Bucket: {Bucket}", _storageConfig.FirebaseStorageBucket);
            _logger.LogInformation("Max Image Width: {Width}", _storageConfig.MaxImageWidth);
            _logger.LogInformation("Max Image Height: {Height}", _storageConfig.MaxImageHeight);
            _logger.LogInformation("Max File Size: {Size} bytes", _storageConfig.MaxFileSize);
            _logger.LogInformation("Image Quality: {Quality}", _storageConfig.ImageQuality);
            _logger.LogInformation("Thumbnail Size: {Size}", _storageConfig.ThumbnailSize);
        }


        #region Core Image Processing (using SkiaSharp)

        /// <summaryLogMediaServiceConfiguration
        /// Processes image data (resize, compress) using SkiaSharp.
        /// </summary>
        /// <param name="imageData">Original image data.</param>
        /// <param name="maxWidth">Maximum width.</param>
        /// <param name="maxHeight">Maximum height.</param>
        /// <param name="quality">JPEG quality (0-100).</param>
        /// <returns>Processed image data as byte array.</returns>
        public Task<byte[]> ProcessImageAsync(byte[] imageData, int maxWidth, int maxHeight, int quality = 80)
        {
            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning("ProcessImageAsync called with empty image data.");
                return Task.FromResult(Array.Empty<byte>());
            }

            try
            {
                using var inputStream = new MemoryStream(imageData);
                using var processedStream = ProcessImageInternalAsync(inputStream, maxWidth, maxHeight, quality);
                return Task.FromResult(processedStream.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image data.");
                return Task.FromResult(Array.Empty<byte>()); // Return empty array on error
            }
        }

        /// <summary>
        /// Generates a square thumbnail from an image stream using SkiaSharp.
        /// </summary>
        /// <param name="imageStream">Input image stream.</param>
        /// <param name="thumbnailSize">Desired size of the square thumbnail.</param>
        /// <param name="quality">JPEG quality (0-100).</param>
        /// <returns>Thumbnail image data as byte array.</returns>
        public Task<byte[]> GenerateThumbnailAsync(Stream imageStream, int thumbnailSize = 150, int quality = 70)
        {
            if (imageStream == null || !imageStream.CanRead)
            {
                _logger.LogWarning("GenerateThumbnailAsync called with invalid stream.");
                return Task.FromResult(Array.Empty<byte>());
            }

            try
            {
                imageStream.Position = 0; // Ensure stream is at the beginning
                using var processedStream = GenerateThumbnailInternalAsync(imageStream, thumbnailSize, quality);
                return Task.FromResult(processedStream.ToArray());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating thumbnail.");
                return Task.FromResult(Array.Empty<byte>()); // Return empty array on error
            }
        }

        // --- Internal SkiaSharp Helpers ---

        /// <summary>
        /// Internal helper to process image stream using SkiaSharp.
        /// </summary>
        private MemoryStream ProcessImageInternalAsync(Stream imageStream, int maxWidth, int maxHeight, int quality)
        {
            imageStream.Position = 0;
            using var original = SKBitmap.Decode(imageStream);
            if (original == null)
            {
                throw new InvalidOperationException("Failed to decode image stream.");
            }

            int newWidth = original.Width;
            int newHeight = original.Height;

            // Calculate new dimensions while maintaining aspect ratio
            if (newWidth > maxWidth || newHeight > maxHeight)
            {
                double ratioX = (double)maxWidth / newWidth;
                double ratioY = (double)maxHeight / newHeight;
                double ratio = Math.Min(ratioX, ratioY);
                newWidth = (int)Math.Round(newWidth * ratio);
                newHeight = (int)Math.Round(newHeight * ratio);
            }

            // Resize if necessary
            SKBitmap resized = original;
            if (newWidth != original.Width || newHeight != original.Height)
            {
                _logger.LogDebug("Resizing image from {OriginalWidth}x{OriginalHeight} to {NewWidth}x{NewHeight}", original.Width, original.Height, newWidth, newHeight);
                resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                original.Dispose(); // Dispose original if resized
            }

            // Encode to JPEG
            var outputStream = new MemoryStream();
            using (var image = SKImage.FromBitmap(resized)) // Dispose SKImage
            {
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality); // Dispose SKData
                data.SaveTo(outputStream);
            }
            resized.Dispose(); // Dispose resized (or original if not resized)

            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Internal helper to generate thumbnail stream using SkiaSharp.
        /// </summary>
        private MemoryStream GenerateThumbnailInternalAsync(Stream imageStream, int thumbnailSize, int quality)
        {
            imageStream.Position = 0;
            using var original = SKBitmap.Decode(imageStream);
            if (original == null)
            {
                throw new InvalidOperationException("Failed to decode image stream for thumbnail.");
            }

            // Calculate cropping/scaling dimensions
            float scale = Math.Max((float)thumbnailSize / original.Width, (float)thumbnailSize / original.Height);
            float scaledWidth = original.Width * scale;
            float scaledHeight = original.Height * scale;
            float cropX = (scaledWidth - thumbnailSize) / 2f;
            float cropY = (scaledHeight - thumbnailSize) / 2f;

            _logger.LogDebug("Generating thumbnail: scale={Scale}, cropX={CropX}, cropY={CropY}", scale, cropX, cropY);

            // Create thumbnail bitmap and canvas
            var thumbnailInfo = new SKImageInfo(thumbnailSize, thumbnailSize);
            using var surface = SKSurface.Create(thumbnailInfo);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.White); // Optional: Background color

            // Draw the scaled and centered image onto the thumbnail canvas
            canvas.DrawBitmap(original,
                              new SKRect(-cropX, -cropY, scaledWidth - cropX, scaledHeight - cropY), // Source rect (adjust for crop)
                              new SKRect(0, 0, thumbnailSize, thumbnailSize)); // Destination rect


            // Encode to JPEG
            var outputStream = new MemoryStream();
            using (var image = surface.Snapshot()) // Get image from surface
            {
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                data.SaveTo(outputStream);
            }

            outputStream.Position = 0;
            return outputStream;
        }

        #endregion

        #region Upload Methods

        /// <summary>
        /// Uploads an image from a Stream after processing.
        /// </summary>
        public async Task<(string downloadUrl, string storagePath)> UploadImageAsync(Stream imageStream, string fileName, int maxWidth = 1200, int maxHeight = 1200, int quality = 80)
        {
            if (imageStream == null || !imageStream.CanRead)
            {
                throw new ArgumentNullException(nameof(imageStream), "Input stream cannot be null or unreadable.");
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentNullException(nameof(fileName), "File name cannot be empty.");
            }

            _logger.LogInformation("Starting image upload for: {FileName}", fileName);

            try
            {
                // Process the image (resize/compress)
                using var processedStream = ProcessImageInternalAsync(imageStream, maxWidth, maxHeight, quality);
                if (processedStream == null || processedStream.Length == 0)
                {
                    _logger.LogError("Image processing failed for {FileName}", fileName);
                    throw new InvalidOperationException("Image processing failed.");
                }

                _logger.LogDebug("Image processed, size: {Size} bytes", processedStream.Length);

                // Generate unique storage path
                var storagePath = GenerateStoragePath(fileName);
                _logger.LogDebug("Generated storage path: {StoragePath}", storagePath);

                // Upload to Firebase Storage
                var uploadTask = _firebaseStorage
                    .Child(storagePath)
                    .PutAsync(processedStream); // Upload the processed stream

                // Get download URL
                string downloadUrl = await uploadTask;
                _logger.LogInformation("Image uploaded successfully: {FileName} to {StoragePath}", fileName, storagePath);
                _logger.LogDebug("Download URL: {DownloadUrl}", downloadUrl);

                return (downloadUrl, storagePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image stream: {FileName}", fileName);
                throw; // Re-throw exception to indicate failure
            }
        }

        /// <summary>
        /// Uploads an image from a byte array after processing.
        /// </summary>
        public async Task<(string downloadUrl, string storagePath)> UploadImageAsync(byte[] imageData, string fileName, int maxWidth = 1200, int maxHeight = 1200, int quality = 80)
        {
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentNullException(nameof(imageData), "Image data cannot be empty.");
            }

            _logger.LogDebug("Uploading image from byte array: {FileName}", fileName);
            using var imageStream = new MemoryStream(imageData);
            return await UploadImageAsync(imageStream, fileName, maxWidth, maxHeight, quality);
        }

        /// <summary>
        /// Uploads an image from a FileResult after processing. Returns only the URL.
        /// </summary>
        public async Task<string> UploadImageAsync(FileResult fileResult, int maxWidth = 1200, int maxHeight = 1200, int quality = 80)
        {
            if (fileResult == null)
            {
                throw new ArgumentNullException(nameof(fileResult));
            }

            _logger.LogDebug("Uploading image from FileResult: {FileName}", fileResult.FileName);
            try
            {
                using var stream = await fileResult.OpenReadAsync();
                var (downloadUrl, _) = await UploadImageAsync(stream, fileResult.FileName, maxWidth, maxHeight, quality);
                return downloadUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image from FileResult: {FileName}", fileResult.FileName);
                return null; // Return null or empty string on failure
            }
        }

        #endregion

        #region Deletion and URL Generation

        /// <summary>
        /// Deletes an image from Firebase Storage using its download URL.
        /// </summary>
        /// <param name="imageUrl">The public download URL of the image.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                _logger.LogWarning("DeleteImageAsync called with empty URL.");
                return false;
            }

            try
            {
                string storagePath = ExtractStoragePathFromUrl(imageUrl);
                if (string.IsNullOrEmpty(storagePath))
                {
                    _logger.LogWarning("Could not extract storage path from URL: {ImageUrl}", imageUrl);
                    return false;
                }

                _logger.LogInformation("Attempting to delete image at storage path: {StoragePath}", storagePath);
                await _firebaseStorage
                    .Child(storagePath)
                    .DeleteAsync();

                _logger.LogInformation("Successfully deleted image: {StoragePath}", storagePath);
                return true;
            }
            catch (Firebase.Storage.FirebaseStorageException fsex) when (fsex.InnerException is System.Net.Http.HttpRequestException httpEx && httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // Handle case where file doesn't exist gracefully
                _logger.LogWarning("Image not found during deletion (or already deleted): {ImageUrl}", imageUrl);
                return true; // Consider it successful if it's already gone
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image from URL: {ImageUrl}", imageUrl);
                return false;
            }
        }

        /// <summary>
        /// Generates a secure (token-based) download URL for a file in Firebase Storage.
        /// Note: Firebase URLs are inherently token-based. True time-limited URLs typically require Cloud Functions.
        /// </summary>
        /// <param name="storagePath">The path to the file in Firebase Storage.</param>
        /// <param name="expiration">This parameter is currently ignored as Firebase URLs don't have built-in time limits in the same way as signed URLs from other providers.</param>
        /// <returns>The download URL.</returns>
        public async Task<string> GenerateSecureUrlAsync(string storagePath, TimeSpan expiration)
        {
            // Note: Firebase Storage URLs include an access token but aren't inherently time-limited
            // like typical signed URLs. Generating truly time-limited URLs often involves
            // Cloud Functions or a custom token verification system.
            // This implementation returns the standard download URL.
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                throw new ArgumentNullException(nameof(storagePath));
            }

            _logger.LogDebug("Generating download URL for storage path: {StoragePath}", storagePath);

            try
            {
                string url = await _firebaseStorage
                    .Child(storagePath)
                    .GetDownloadUrlAsync();

                _logger.LogDebug("Generated URL: {Url}", url);
                return url;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure URL for {StoragePath}", storagePath);
                throw;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Generates a unique storage path for a file.
        /// Example: images/20231027103000/guid_sanitized_filename.jpg
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <returns>Unique storage path.</returns>
        private string GenerateStoragePath(string fileName)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            var sanitizedFileName = SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)); // Sanitize name only
            var extension = Path.GetExtension(fileName).ToLowerInvariant(); // Keep original extension

            // Recommended: Use folders for organization
            return $"images/{timestamp}/{uniqueId}_{sanitizedFileName}{extension}";
        }

        /// <summary>
        /// Sanitizes a file name to remove characters invalid for Firebase paths.
        /// Firebase disallows: . $ # [ ] /
        /// </summary>
        /// <param name="fileName">Original file name.</param>
        /// <returns>Sanitized file name.</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "file";

            // Define invalid characters (Firebase specific)
            char[] invalidChars = { '.', '$', '#', '[', ']', '/' };

            // Replace invalid characters with an underscore
            string sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

            // Optional: Trim and handle potential empty results
            sanitized = sanitized.Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "file" : sanitized;
        }

        /// <summary>
        /// Extracts the storage path from a Firebase Storage download URL.
        /// Example URL: https://firebasestorage.googleapis.com/v0/b/your-bucket.appspot.com/o/images%2Ftest.jpg?alt=media&token=...
        /// Desired Path: images/test.jpg
        /// </summary>
        /// <param name="imageUrl">Firebase Storage download URL.</param>
        /// <returns>The storage path or null if parsing fails.</returns>
        private string ExtractStoragePathFromUrl(string imageUrl)
        {
            try
            {
                if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
                {
                    return null;
                }

                // Expected path format: /v0/b/{bucket}/o/{encoded_path}
                string pathAndQuery = uri.PathAndQuery; // Gets "/v0/b/..."
                string prefix = $"/v0/b/{_storageConfig.FirebaseStorageBucket}/o/";

                int pathStart = pathAndQuery.IndexOf(prefix);
                if (pathStart < 0)
                {
                    _logger.LogWarning("URL does not match expected Firebase Storage format (prefix not found): {Url}", imageUrl);
                    return null;
                }

                pathStart += prefix.Length; // Move index past the prefix

                int queryStart = pathAndQuery.IndexOf('?', pathStart);
                string encodedPath = queryStart >= 0
                    ? pathAndQuery.Substring(pathStart, queryStart - pathStart)
                    : pathAndQuery.Substring(pathStart);

                // URL Decode the path
                string storagePath = WebUtility.UrlDecode(encodedPath);

                return storagePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse storage path from URL: {ImageUrl}", imageUrl);
                return null;
            }
        }

        #endregion
    }
}