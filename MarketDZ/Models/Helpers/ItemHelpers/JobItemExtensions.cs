// Models/ItemHelpers/JobItemExtensions.cs
using System;
using System.Linq;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    /// <summary>
    /// Extension methods for Job items
    /// </summary>
    public static class JobItemExtensions
    {
        /// <summary>
        /// Checks if an item is a Job item
        /// </summary>
        public static bool IsJobItem(this Item item)
        {
            return item.Category == ItemCategory.Job;
        }

        /// <summary>
        /// Validates a Job item's data
        /// </summary>
        public static bool ValidateJobItem(this Item item, out string errorMessage)
        {
            errorMessage = string.Empty;

            // Ensure it's a Job item
            if (!item.IsJobItem())
            {
                errorMessage = "Item is not a Job listing";
                return false;
            }

            // Validate Job category is set
            if (!item.JobCategory.HasValue)
            {
                errorMessage = "Job category is required";
                return false;
            }

            // Validate company name
            if (string.IsNullOrEmpty(item.CompanyName))
            {
                errorMessage = "Company name is required";
                return false;
            }

            // Validate apply method
            if (!item.ApplyMethod.HasValue)
            {
                errorMessage = "Application method is required";
                return false;
            }

            // Validate apply contact based on apply method
            if (string.IsNullOrEmpty(item.ApplyContact))
            {
                errorMessage = "Application contact information is required";
                return false;
            }
            else
            {
                // Specific validation based on application method
                switch (item.ApplyMethod.Value)
                {
                    case ApplyMethod.Email:
                        if (!item.ApplyContact.Contains('@'))
                        {
                            errorMessage = "Valid email address is required for email application method";
                            return false;
                        }
                        break;
                    case ApplyMethod.URL:
                        if (!item.ApplyContact.StartsWith("http"))
                        {
                            errorMessage = "Valid URL is required for website application method";
                            return false;
                        }
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets a formatted job type and category display
        /// </summary>
        public static string GetJobTypeDisplay(this Item item)
        {
            if (!item.IsJobItem())
                return string.Empty;

            string jobType = item.JobType ?? "Not specified";
            string jobCategory = item.JobCategory.HasValue
                ? item.JobCategory.Value.ToString().Replace("_", " ")
                : "General";

            return $"{jobType} - {jobCategory}";
        }

        /// <summary>
        /// Gets how to apply instructions
        /// </summary>
        public static string GetHowToApply(this Item item)
        {
            if (!item.IsJobItem() || !item.ApplyMethod.HasValue)
                return string.Empty;

            switch (item.ApplyMethod.Value)
            {
                case ApplyMethod.Email:
                    return $"Apply by email: {item.ApplyContact}";
                case ApplyMethod.PhoneNumber:
                    return $"Apply by phone: {item.ApplyContact}";
                case ApplyMethod.URL:
                    return $"Apply online: {item.ApplyContact}";
                default:
                    return $"Apply using: {item.ApplyContact}";
            }
        }
    }
}