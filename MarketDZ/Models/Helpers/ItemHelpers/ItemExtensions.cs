// Models/ItemHelpers/ItemExtensions.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Hel;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    /// <summary>
    /// General extension methods for all item types
    /// </summary>
    public static class ItemExtensions
    {
        // Other methods remain unchanged...

        /// <summary>
        /// Gets a short summary of the item for display in lists
        /// </summary>
        public static string GetShortSummary(this Item item)
        {
            // Explicitly qualify the method call to resolve ambiguity
            string priceText = ItemExtensions.GetFormattedPrice(item);
            string categoryText = item.GetSubcategoryDisplay();
            string locationText = item.State?.ToString().Replace("_", " ") ?? "";

            return $"{categoryText} • {priceText}{(string.IsNullOrEmpty(locationText) ? "" : $" • {locationText}")}";
        }

        internal static string GetMainPhotoUrl(Item item)
        {
            throw new NotImplementedException();
        }

        private static string GetFormattedPrice(Item item)
        {
            throw new NotImplementedException();
        }
    }
}