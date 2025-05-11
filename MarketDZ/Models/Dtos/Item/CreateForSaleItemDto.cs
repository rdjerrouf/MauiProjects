// CreateForSaleItemDto.cs (updated)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    public class CreateForSaleItemDto : CreateItemDto
    {
        public CreateForSaleItemDto()
        {
            // Update to use enum instead of string
            Category = ItemCategory.ForSale;
        }

        // Sale-specific properties
        public ForSaleCategory? ForSaleCategory { get; set; }
        public string? Condition { get; set; }
        public bool IsNegotiable { get; set; } = true;

        // Photo properties for sale items
        public List<string>? PhotoUrls { get; set; }
        public string? PhotoUrl { get; set; }
        public ICollection<FileResult>? Photos { get; set; }
    }
}