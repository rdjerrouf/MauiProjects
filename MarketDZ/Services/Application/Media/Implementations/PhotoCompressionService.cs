using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SkiaSharp;
namespace MarketDZ.Services.Application.Media.Implementations
{
    public class PhotoCompressionService : IPhotoCompressionService
    {
        private readonly ILogger<PhotoCompressionService> _logger;
        private const int MAX_WIDTH = 1920;  // Full HD width
        private const int MAX_HEIGHT = 1080; // Full HD height
        private const int JPEG_QUALITY = 80; // 80% quality is usually a good balance
        private const float MAX_FILE_SIZE_MB = 1.0f; // Target maximum file size after compression

        public PhotoCompressionService(ILogger<PhotoCompressionService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Stream> CompressPhotoAsync(Stream originalPhotoStream, string fileName)
        {
            try
            {
                using var inputStream = new SKManagedStream(originalPhotoStream);
                using var original = SKBitmap.Decode(inputStream);

                if (original == null)
                {
                    throw new InvalidOperationException("Failed to decode image");
                }

                // Calculate new dimensions maintaining aspect ratio
                var (newWidth, newHeight) = CalculateNewDimensions(original.Width, original.Height);

                // Create resized image if needed
                SKBitmap resized = original;
                if (newWidth != original.Width || newHeight != original.Height)
                {
                    resized = original.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                }

                // Convert to JPEG with compression
                var compressedStream = new MemoryStream();
                using (var image = SKImage.FromBitmap(resized))
                {
                    var data = image.Encode(SKEncodedImageFormat.Jpeg, JPEG_QUALITY);
                    data.SaveTo(compressedStream);
                }

                // Check if we need further compression based on file size
                if (compressedStream.Length > MAX_FILE_SIZE_MB * 1024 * 1024)
                {
                    // Further reduce quality if still too large
                    var reducedQuality = JPEG_QUALITY;
                    while (compressedStream.Length > MAX_FILE_SIZE_MB * 1024 * 1024 && reducedQuality > 50)
                    {
                        reducedQuality -= 10;
                        compressedStream.SetLength(0);

                        using (var image = SKImage.FromBitmap(resized))
                        {
                            var data = image.Encode(SKEncodedImageFormat.Jpeg, reducedQuality);
                            data.SaveTo(compressedStream);
                        }
                    }
                }

                compressedStream.Position = 0;
                return compressedStream;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compressing photo");
                throw;
            }
        }

        private (int width, int height) CalculateNewDimensions(int originalWidth, int originalHeight)
        {
            if (originalWidth <= MAX_WIDTH && originalHeight <= MAX_HEIGHT)
            {
                return (originalWidth, originalHeight);
            }

            float aspectRatio = (float)originalWidth / originalHeight;

            if (originalWidth > originalHeight)
            {
                int newWidth = Math.Min(originalWidth, MAX_WIDTH);
                int newHeight = (int)(newWidth / aspectRatio);
                return (newWidth, newHeight);
            }
            else
            {
                int newHeight = Math.Min(originalHeight, MAX_HEIGHT);
                int newWidth = (int)(newHeight * aspectRatio);
                return (newWidth, newHeight);
            }
        }
       

    }
}
