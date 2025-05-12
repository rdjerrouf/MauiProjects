

using MarketDZ.Models.Core.Infrastructure;

namespace MarketDZ.Models.Core.Entities
{
    public class ItemPhoto : IEntity
    {
        public string Id { get; set; } = string.Empty; 
        public string? ItemId { get; set; }
        public Item? Item { get; set; }
        public required string PhotoUrl { get; set; }
        public bool IsPrimaryPhoto { get; set; }
        public string? StoragePath { get; set; }
        public DateTime UploadedAt { get; set; }
        public string? DisplayOrder { get; set; }
        public string? Caption { get; set; }
        public int Version { get; internal set; }
        public DateTime LastModified { get; internal set; }
    }
}