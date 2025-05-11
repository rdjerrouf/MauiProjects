// Models/ItemHelpers/ForSaleItemExtensions.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    /// <summary>
    /// Extension methods for For Sale items
    /// </summary>
    public static class ForSaleItemExtensions
    {
        /// <summary>
        /// Checks if an item is a For Sale item
        /// </summary>
        public static bool IsForSaleItem(this Item item)
        {
            return item.Category == ItemCategory.ForSale;
        }

        /// <summary>
        /// Validates a For Sale item's data
        /// </summary>
        public static bool ValidateForSaleItem(this Item item, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Ensure it's a For Sale item
            if (!item.IsForSaleItem())
            {
                errorMessage = "Item is not a For Sale item";
                return false;
            }

            // Validate For Sale category is set
            if (!item.ForSaleCategory.HasValue)
            {
                errorMessage = "For Sale category is required";
                return false;
            }

            // Price validation
            if (item.Price <= 0)
            {
                errorMessage = "Price must be greater than zero";
                return false;
            }

            // Additional For Sale specific validations could go here

            return true;
        }

        /// <summary>
        /// Gets a display string for a For Sale item's condition
        /// </summary>
        public static string GetConditionDisplay(this Item item)
        {
            if (!item.IsForSaleItem())
                return string.Empty;

            // This could be enhanced with a proper condition enum instead of string
            return !string.IsNullOrEmpty(item.Condition) ? item.Condition : "Not specified";
        }
    }
}