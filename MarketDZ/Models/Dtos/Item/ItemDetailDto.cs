using System.Collections.Generic;
using MarketDZ.Models.Dtos.Item;

namespace MarketDZ.Models.Dtos
{
    namespace MarketDZ.Models.Dtos.Item
    {
        public string?  ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? Category { get; set; }
        public string? State { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public List<PhotoDto> Photos { get; set; } = new();
        public DateTime ListedDate { get; set; }
        public string? PostedByUser { get; set; }
        public string?  PostedByUserId { get; set; }
    }
}