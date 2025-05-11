// Item.cs (updated with missing properties)
using System.ComponentModel.DataAnnotations;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Helpers.ItemHelpers;
using Newtonsoft.Json;

namespace MarketDZ.Models.Core.Entities
{
    public class Item : IEntity
    {
        // Update the Id property to match the nullability of the IEntity interface
        public string Id { get; set; } = string.Empty;
        public required string Title { get; set; }
        public required string Description { get; set; }

        // Changed from string to enum
        public ItemCategory Category { get; set; }

        public List<string> PhotoUrls { get; set; } = new List<string>();
        public string? PhotoUrl { get; set; }
        public DateTime ListedDate { get; set; } = DateTime.UtcNow;
        [Range(0, 999999.99)]
        public required decimal Price { get; set; } = 0.00M;

        // Photo properties
        public ICollection<ItemPhoto> Photos { get; set; } = new List<ItemPhoto>();

        // Updated to use enum
        public int MaxPhotos => Category switch
        {
            ItemCategory.ForSale => 5,  // 5 photos for sale items
            ItemCategory.ForRent => 8,  // 8 photos for rent items
            _ => 3            // 3 photos default for other categories
        };

        // Updated to use enum
        public bool SupportsPhotos =>
            Category == ItemCategory.ForSale ||
            Category == ItemCategory.ForRent;

        // Location properties
        public AlState? State { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public ItemLocation? ItemLocation { get; set; }
        public bool HasLocation => ItemLocation != null;

        // ForSale-specific properties
        public ForSaleCategory? ForSaleCategory { get; set; }
        public string? Condition { get; set; }  // Added this missing property
        public bool IsNegotiable { get; set; } = true;  // Added this missing property

        // ForRent-specific properties
        public ForRentCategory? ForRentCategory { get; set; }
        public string? RentalPeriod { get; set; }
        public DateTime? AvailableFrom { get; set; }
        public DateTime? AvailableTo { get; set; }
        public DateTime? LastModified { get; set; }
        // Job-specific properties
        public JobCategory? JobCategory { get; set; }
        public string? JobType { get; set; }
        public string? CompanyName { get; set; }
        public string? JobLocation { get; set; }
        public ApplyMethod? ApplyMethod { get; set; }
        public string? ApplyContact { get; set; }

        // Service-specific properties
        public ServiceCategory? ServiceCategory { get; set; }
        public ServiceAvailability? ServiceAvailability { get; set; }
        public int? YearsOfExperience { get; set; }
        public int? NumberOfEmployees { get; set; }
        public string? ServiceType { get; set; }
        public string? ServiceLocation { get; set; }
        public double? AverageRating { get; set; }

        // User relationship
        [JsonProperty("postedByUserId")]
        public string?  PostedByUserId { get; set; }
        [JsonProperty("postedByUser")]
        public  User? PostedByUser { get; set; }
        public ICollection<User> FavoritedByUsers { get; set; } = new List<User>();

        // Status and counts
        public ItemStatus Status { get; set; } = ItemStatus.Active;
        public int ViewCount { get; set; }
        public int InquiryCount { get; set; }
        public int RatingCount { get; set; }
        public int FavoriteCount { get; set; }

        // Additional properties
        public string? ImageUrl { get; set; }
        public string? ImagePath { get; set; }
        public bool IsSalaryDisclosed { get; set; } = true;
        public int Version { get; set; }

        /// <summary>
        /// Gets the main photo URL for display
        /// </summary>
        /// <summary>
        /// Gets the main photo URL for display
        /// </summary>
        public string GetMainPhotoUrl() => ItemExtensions.GetMainPhotoUrl(this);

        /// <summary>
        /// Validates this item based on its category
        /// </summary>
        /// <summary>
        /// Validates this item based on its category
        /// </summary>
        public bool Validate(out string errorMessage)
        {
            // First validate common fields - use CommonItemHelpers instead of ItemExtensions
            if (!CommonItemHelpers.ValidateCommonFields(this, out errorMessage))
                return false;

            // Then validate category-specific fields
            switch (Category)
            {
                case ItemCategory.ForSale:
                    return this.ValidateForSaleItem(out errorMessage);

                case ItemCategory.ForRent:
                    return this.ValidateRentalItem(out errorMessage);

                case ItemCategory.Job:
                    return this.ValidateJobItem(out errorMessage);

                case ItemCategory.Service:
                    return this.ValidateServiceItem(out errorMessage);

                default:
                    errorMessage = "Unknown item category";
                    return false;
            }
        }
    }
}