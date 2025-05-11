using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.Item
{
    public class CreateJobItemDto : CreateItemDto
    {
        public CreateJobItemDto()
        {
            // Update to use enum instead of string
            Category = ItemCategory.Job;
        }


        // Job-specific properties
        public string? JobType { get; set; }
        public JobCategory? JobCategory { get; set; }
        public string? CompanyName { get; set; }
        public string? JobLocation { get; set; }
        public ApplyMethod? ApplyMethod { get; set; }
        public string? ApplyContact { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public string? SalaryPeriod { get; set; }
        public bool IsSalaryDisclosed { get; set; } = true;
    }
}