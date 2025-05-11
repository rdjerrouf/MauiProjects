using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Infrastructure.Firebase.Indexes
{
    /// <summary>
    /// Category-to-Item index entry for category browsing
    /// </summary>
    public class CategoryItemIndex : FirebaseIndex
    {
        /// <summary>
        /// The category name or ID
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// The item ID
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// When this relationship was created
        /// </summary>
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Creates a new category-item index entry
        /// </summary>
        public CategoryItemIndex() { }

        /// <summary>
        /// Creates a new category-item index entry
        /// </summary>
        public CategoryItemIndex(string category, string itemId)
        {
            Category = category;
            ItemId = itemId;
            TargetId = itemId; // Set TargetId to ItemId for standard base class behavior
        }

        /// <summary>
        /// Generates the path for this index in Firebase
        /// </summary>
        public string GetIndexPath()
        {
            return $"items_by_category/{Category}/{ItemId}";
        }
    }

}
