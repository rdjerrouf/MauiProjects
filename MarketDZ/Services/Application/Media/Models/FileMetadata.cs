


namespace MarketDZ.Services.Application.Media.Models

{
    public class FileMetadata
    {
        public string? StoragePath { get; set; }
        public string? OriginalFileName { get; set; }
        public string? DownloadUrl { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? ContentHash { get; set; }
    }


}
