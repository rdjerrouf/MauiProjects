using MarketDZ.Services.Media;

namespace MarketDZ.Services.Application.Media.Implementations
{
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

            using (var memoryStream = new MemoryStream(imageData))
            {
                var (downloadUrl, _) = await service.UploadImageAsync(memoryStream, fileName);
                return downloadUrl;
            }
        }
    }
}
