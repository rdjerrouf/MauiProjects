// ItemUpdateDto.cs (updated)
using System;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    /// <summary>
    /// Data Transfer Object for updating an existing item
    /// </summary>
    public class ItemUpdateDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }

        // Changed from string to enum, made nullable
        public ItemCategory? Category { get; set; }

        // Job properties
        public string? JobType { get; set; }
        public JobCategory? JobCategory { get; set; }
        public string? CompanyName { get; set; }
        public string? JobLocation { get; set; }
        public ApplyMethod? ApplyMethod { get; set; }
        public string? ApplyContact { get; set; }

        // Service properties
        public string? ServiceType { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }
        public ServiceAvailability? ServiceAvailability { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? ServiceLocation { get; set; }

        // Rental properties
        public string? RentalPeriod { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public ForRentCategory? ForRentCategory { get; set; }

        // Sale properties
        public ForSaleCategory? ForSaleCategory { get; set; }

        // Photos for image-supporting items
        public ICollection<FileResult>? Photos { get; set; }
        public ICollection<string>? PhotoUrls { get; set; }

        // Location
        public AlState? State { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}