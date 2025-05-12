
using MarketDZ.Services.Application.Media.Interfaces;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Extensions
{
    /// <summary>
    /// Extension methods for the IMediaService interface
    /// </summary>
    public static class MediaServiceExtensions
    {
        public static async Task<string> UploadUserImageAsync(this IMediaService service, byte[] imageData, string fileName)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (imageData == null || imageData.Length == 0)
            {
                throw new ArgumentException("Image data cannot be null or empty", nameof(imageData));
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            // Use proper explicit type declarations for tuple deconstruction
            var result = await service.UploadImageAsync(imageData, fileName);
            string downloadUrl = result.downloadUrl;
            return downloadUrl;
        }

        // Add an overload that handles stream input
        public static async Task<string> UploadUserImageAsync(this IMediaService service, Stream imageStream, string fileName)
        {
            if (service == null)
            {
                throw new ArgumentNullException(nameof(service));
            }
            if (imageStream == null || !imageStream.CanRead)
            {
                throw new ArgumentException("Image stream cannot be null or must be readable", nameof(imageStream));
            }
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));
            }

            // Use proper explicit type declarations for tuple deconstruction
            var result = await service.UploadImageAsync(imageStream, fileName);
            string downloadUrl = result.downloadUrl;
            return downloadUrl;
        }
    }
}