// ItemCategoryExtensions.cs
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    public static class ItemCategoryExtensions
    {
        /// <summary>
        /// Converts a string category to ItemCategory enum
        /// </summary>
        public static ItemCategory ToItemCategory(this string category)
        {
            if (string.IsNullOrEmpty(category))
                return ItemCategory.ForSale; // Default

            // Try to parse as enum
            if (Enum.TryParse<ItemCategory>(category, true, out var result))
                return result;

            // Handle legacy string formats with spaces
            return category.ToLowerInvariant() switch
            {
                "for sale" => ItemCategory.ForSale,
                "for rent" => ItemCategory.ForRent,
                "job" or "jobs" => ItemCategory.Job,
                "service" or "services" => ItemCategory.Service,
                _ => ItemCategory.ForSale // Default to ForSale if unknown
            };
        }

        /// <summary>
        /// Converts an ItemCategory enum to the legacy string format
        /// </summary>
        public static string ToLegacyString(this ItemCategory category)
        {
            return category switch
            {
                ItemCategory.ForSale => "For Sale",
                ItemCategory.ForRent => "For Rent",
                ItemCategory.Job => "Jobs",
                ItemCategory.Service => "Services",
                _ => "For Sale"
            };
        }
    }
}