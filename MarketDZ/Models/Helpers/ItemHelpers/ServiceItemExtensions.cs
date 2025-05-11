// Models/ItemHelpers/ServiceItemExtensions.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    /// <summary>
    /// Extension methods for Service items
    /// </summary>
    public static class ServiceItemExtensions
    {
        /// <summary>
        /// Checks if an item is a Service item
        /// </summary>
        public static bool IsServiceItem(this Item item)
        {
            return item.Category == ItemCategory.Service;
        }

        /// <summary>
        /// Validates a Service item's data
        /// </summary>
        public static bool ValidateServiceItem(this Item item, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Ensure it's a Service item
            if (!item.IsServiceItem())
            {
                errorMessage = "Item is not a Service listing";
                return false;
            }

            // Validate Service category is set
            if (!item.ServiceCategory.HasValue)
            {
                errorMessage = "Service category is required";
                return false;
            }

            // Validate service type
            if (string.IsNullOrEmpty(item.ServiceType))
            {
                errorMessage = "Service type is required";
                return false;
            }

            // Validate service availability
            if (!item.ServiceAvailability.HasValue)
            {
                errorMessage = "Service availability information is required";
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

        /// <summary>
        /// Gets a formatted experience description
        /// </summary>
        public static string GetExperienceDescription(this Item item)
        {
            if (!item.IsServiceItem())
                return string.Empty;

            if (item.YearsOfExperience.HasValue)
            {
                int years = item.YearsOfExperience.Value;
                return years == 1 ? "1 year of experience" : $"{years} years of experience";
            }

            return "Experience not specified";
        }

        /// <summary>
        /// Gets a formatted service availability description
        /// </summary>
        public static string GetAvailabilityDescription(this Item item)
        {
            if (!item.IsServiceItem() || !item.ServiceAvailability.HasValue)
                return string.Empty;

            switch (item.ServiceAvailability.Value)
            {
                case ServiceAvailability.FullTime:
                    return "Available full-time (regular business hours)";
                case ServiceAvailability.PartTime:
                    return "Available part-time (limited hours)";
                case ServiceAvailability.Weekends:
                    return "Available on weekends";
                case ServiceAvailability.Evenings:
                    return "Available during evenings";
                case ServiceAvailability.Flexible:
                    return "Flexible availability (based on your needs)";
                default:
                    return "Availability not specified";
            }
        }

        /// <summary>
        /// Gets business size description based on number of employees
        /// </summary>
        public static string GetBusinessSizeDescription(this Item item)
        {
            if (!item.IsServiceItem() || !item.NumberOfEmployees.HasValue)
                return string.Empty;

            int employees = item.NumberOfEmployees.Value;

            if (employees == 1)
                return "Individual provider";
            else if (employees <= 5)
                return "Small team";
            else if (employees <= 20)
                return "Medium-sized business";
            else
                return "Large business";
        }
    }
}