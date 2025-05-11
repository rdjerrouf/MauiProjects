using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    public class ItemListDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; } 
        public decimal Price { get; set; }
        public string? MainPhotoUrl { get; set; }
        public DateTime ListedDate { get; set; }
        public string? Category { get; set; }
        public string? PrimaryPhotoUrl { get; set; }
        public AlState? State { get; set; }
        public ItemStatus Status { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? PhotoUrl { get; internal set; }
    }
}
