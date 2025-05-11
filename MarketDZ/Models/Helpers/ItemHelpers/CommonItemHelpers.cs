// Models/ItemHelpers/ItemHelpers.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Hel
{
    /// <summary>
    /// General helper methods for all item types
    /// </summary>
    public static class CommonItemHelpers
    { 
        /// <summary>
        /// Gets a formatted price string
        /// </summary>
        public static string GetFormattedPrice(this Item item)
        {
            // Different formatting based on category
            switch (item.Category)
            {
                case ItemCategory.ForRent:
                    return $"{item.Price.ToString("C")} per {item.RentalPeriod ?? "period"}";

                case ItemCategory.Job:
                    if (!item.IsSalaryDisclosed)
                        return "Salary not disclosed";
                    return $"{item.Price.ToString("C")} {item.RentalPeriod ?? ""}";

                default:
                    return item.Price.ToString("C");
            }
        }

        /// <summary>
        /// Gets the main photo URL for an item
        /// </summary>
        public static string GetMainPhotoUrl(this Item item)
        {
            // First try to find the primary photo in the Photos collection
            var primaryPhoto = item.Photos?.FirstOrDefault(p => p.IsPrimaryPhoto);
            if (primaryPhoto != null)
                return primaryPhoto.PhotoUrl;

            // Then try the first photo in the collection
            var firstPhoto = item.Photos?.FirstOrDefault();
            if (firstPhoto != null)
                return firstPhoto.PhotoUrl;

            // Then try PhotoUrl property
            if (!string.IsNullOrEmpty(item.PhotoUrl))
                return item.PhotoUrl;

            // Then try the first photo URL in the collection
            var firstPhotoUrl = item.PhotoUrls?.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstPhotoUrl))
                return firstPhotoUrl;

            // Finally, fallback to a default placeholder
            return "default_item_placeholder.png";
        }

        /// <summary>
        /// Gets a display-friendly category name
        /// </summary>
        public static string GetDisplayCategory(this Item item)
        {
            string categoryName = item.Category.ToString();

            // Add spaces before capital letters (e.g., "ForSale" -> "For Sale")
            return System.Text.RegularExpressions.Regex.Replace(
                categoryName,
                "([a-z])([A-Z])",
                "$1 $2"
            );
        }

        /// <summary>
        /// Gets the subcategory name for display
        /// </summary>
        public static string GetSubcategoryDisplay(this Item item)
        {
            switch (item.Category)
            {
                case ItemCategory.ForSale:
                    return item.ForSaleCategory?.ToString().Replace("_", " ") ?? "Other";

                case ItemCategory.ForRent:
                    return item.ForRentCategory?.ToString().Replace("_", " ") ?? "Other";

                case ItemCategory.Job:
                    return item.JobCategory?.ToString().Replace("_", " ") ?? "Other";

                case ItemCategory.Service:
                    return item.ServiceCategory?.ToString().Replace("_", " ") ?? "Other";

                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// Gets a short summary of the item for display in lists
        /// </summary>
        // To resolve the ambiguity, fully qualify the method call to explicitly specify the class where the method is defined.

        public static string GetShortSummary(this Item item)
        {
            string priceText = CommonItemHelpers.GetFormattedPrice(item);
            string categoryText = MarketDZ.Models.ItemHelpers.CommonItemHelpers.GetSubcategoryDisplay(item); // Fully qualified
            string locationText = item.State?.ToString().Replace("_", " ") ?? "";

            return $"{categoryText} • {priceText}{(string.IsNullOrEmpty(locationText) ? "" : $" • {locationText}")}";
        }

        /// <summary>
        /// Validates common fields for all item types
        /// </summary>
        public static bool ValidateCommonFields(this Item item, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Title validation
            if (string.IsNullOrEmpty(item.Title))
            {
                errorMessage = "Title is required";
                return false;
            }

            if (item.Title.Length < 5)
            {
                errorMessage = "Title must be at least 5 characters";
                return false;
            }

            // Description validation
            if (string.IsNullOrEmpty(item.Description))
            {
                errorMessage = "Description is required";
                return false;
            }

            if (item.Description.Length < 20)
            {
                errorMessage = "Description must be at least 20 characters";
                return false;
            }

            // Price validation
            if (item.Price < 0)
            {
                errorMessage = "Price cannot be negative";
                return false;
            }

            return true;
        }
    }
}