// Models/ItemHelpers/RentalItemExtensions.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    /// <summary>
    /// Extension methods for Rental items
    /// </summary>
    public static class RentalItemExtensions
    {
        /// <summary>
        /// Checks if an item is a Rental item
        /// </summary>
        public static bool IsRentalItem(this Item item)
        {
            return item.Category == ItemCategory.ForRent;
        }

        /// <summary>
        /// Validates a Rental item's data
        /// </summary>
        public static bool ValidateRentalItem(this Item item, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Ensure it's a Rental item
            if (!item.IsRentalItem())
            {
                errorMessage = "Item is not a Rental item";
                return false;
            }

            // Validate Rental category is set
            if (!item.ForRentCategory.HasValue)
            {
                errorMessage = "Rental category is required";
                return false;
            }

            // Price validation (per period)
            if (item.Price <= 0)
            {
                errorMessage = "Rental price must be greater than zero";
                return false;
            }

            // Validate rental period
            if (string.IsNullOrEmpty(item.RentalPeriod))
            {
                errorMessage = "Rental period is required";
                return false;
            }

            // Validate date range if both are provided
            if (item.AvailableFrom.HasValue && item.AvailableTo.HasValue)
            {
                if (item.AvailableFrom.Value > item.AvailableTo.Value)
                {
                    errorMessage = "Available from date must be before available to date";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets a formatted availability string
        /// </summary>
        public static string GetAvailabilityPeriod(this Item item)
        {
            if (!item.IsRentalItem())
                return string.Empty;

            if (item.AvailableFrom.HasValue && item.AvailableTo.HasValue)
            {
                return $"Available from {item.AvailableFrom.Value.ToShortDateString()} to {item.AvailableTo.Value.ToShortDateString()}";
            }
            else if (item.AvailableFrom.HasValue)
            {
                return $"Available from {item.AvailableFrom.Value.ToShortDateString()}";
            }
            else
            {
                return "Available now";
            }
        }

        /// <summary>
        /// Gets a formatted price with rental period (e.g., "$500 per month")
        /// </summary>
        public static string GetFormattedRentalPrice(this Item item)
        {
            if (!item.IsRentalItem())
                return item.Price.ToString("C");

            return $"{item.Price.ToString("C")} per {item.RentalPeriod ?? "period"}";
        }
    }
}