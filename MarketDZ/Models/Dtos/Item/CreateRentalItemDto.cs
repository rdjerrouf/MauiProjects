using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    public class CreateRentalItemDto : CreateItemDto
    {
        public CreateRentalItemDto()
        {
            // Update to use enum instead of string
            Category = ItemCategory.ForRent;
        }

        // Rental-specific properties
        public string? RentalPeriod { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public ForRentCategory? ForRentCategory { get; set; }

        // Photo properties for rental items
        public List<string>? PhotoUrls { get; set; }
        public string? PhotoUrl { get; set; }
        public ICollection<FileResult>? Photos { get; set; }
    }
}