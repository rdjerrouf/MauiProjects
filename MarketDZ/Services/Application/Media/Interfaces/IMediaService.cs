using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Media.Interfaces
{
    public interface IMediaService
    {
        Task<byte[]> ProcessImageAsync(byte[] imageData, int maxWidth, int maxHeight, int quality = 80);
        Task<byte[]> GenerateThumbnailAsync(Stream imageStream, int thumbnailSize = 150, int quality = 70);

        // Add overloads that work with Stream input
        Task<(string downloadUrl, string storagePath)> UploadImageAsync(Stream imageStream, string fileName, int maxWidth = 1200, int maxHeight = 1200, int quality = 80);
        Task<(string downloadUrl, string storagePath)> UploadImageAsync(byte[] imageData, string fileName, int maxWidth = 1200, int maxHeight = 1200, int quality = 80);
        Task<string> UploadImageAsync(FileResult fileResult, int maxWidth = 1200, int maxHeight = 1200, int quality = 80);
        Task<bool> DeleteImageAsync(string imageUrl);
        Task<string> GenerateSecureUrlAsync(string storagePath, TimeSpan expiration);
    }
}