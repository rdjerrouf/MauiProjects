using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    public class CreateServiceItemDto : CreateItemDto
    {
        public CreateServiceItemDto()
        {
            // Update to use enum instead of string
            Category = ItemCategory.Service;
        }
        // Service-specific properties
        public string? ServiceType { get; set; }
        public ServiceCategory? ServiceCategory { get; set; }
        public ServiceAvailability? ServiceAvailability { get; set; }
        public int? YearsOfExperience { get; set; }
        public int? NumberOfEmployees { get; set; }
        public string? ServiceLocation { get; set; }
        public bool IsRemoteAvailable { get; set; }
    }
}