using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Dtos.Item
{
    public class ItemPerformanceDto
    {
        public string?  ItemId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int ViewCount { get; set; }
        public int InquiryCount { get; set; }
        public int PerformanceScore { get; set; }
        public double AverageRating { get; set; }
        public DateTime ListedDate { get; set; }
        public ICollection<FileResult> Photos { get; set; } = new List<FileResult>();
    }
}