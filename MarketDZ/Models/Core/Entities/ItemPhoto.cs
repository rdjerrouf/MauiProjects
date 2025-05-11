

namespace MarketDZ.Models.Core.Entities
{
    public class ItemPhoto
    {
        public string?  Id { get; set; }
        public string?  ItemId { get; set; }
        public Item? Item { get; set; }
        public required string PhotoUrl { get; set; }
        public bool IsPrimaryPhoto { get; set; }
        public string? StoragePath { get; set; }
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public string?  DisplayOrder { get; set; }
        public string? Caption { get; set; }
        public int Version { get; internal set; }
        public DateTime LastModified { get; internal set; }
    }
}