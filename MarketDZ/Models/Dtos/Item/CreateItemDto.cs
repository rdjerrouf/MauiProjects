// CreateItemDto.cs (updated)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos
{
    public class CreateItemDto
    {
        // Common properties all items need
        public required string Title { get; set; }
        public required string Description { get; set; }
        public decimal Price { get; set; }

        // Changed from string to enum
        public required ItemCategory Category { get; set; }

        public AlState? State { get; set; }

        // These should probably be internal/protected since they're set by the system
        internal DateTime ListedDate { get; set; }
        internal string? PostedByUserId { get; set; }
        internal User? PostedByUser { get; set; }

        public List<string>? PhotoUrls { get; set; }
        public string? PhotoUrl { get; set; }
        public ICollection<FileResult>? Photos { get; set; }
    }
}