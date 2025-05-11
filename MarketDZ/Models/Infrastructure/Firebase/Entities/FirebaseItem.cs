using System;
using System.Collections.Generic;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using Newtonsoft.Json;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Firebase-specific implementation of the Item model with the new design
    /// </summary>
    public class FirebaseItem : FirebaseEntity, IDomainModelConverter<Item>, IVersionedEntity
    {
        [JsonProperty("version")]
        public int Version { get; set; } = 1;

        [JsonProperty("lastModified")]
        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        // Basic properties
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty; // Stores enum as string
        public string Status { get; set; } = "Active"; // Stores enum as string

        // Primary photo URL for quick display
        public string? PrimaryPhotoUrl { get; set; }

        // References instead of navigation properties
        public string PostedByUserId { get; set; } = string.Empty;

        // Stats
        public int ViewCount { get; set; }
        public int InquiryCount { get; set; }
        public int FavoriteCount { get; set; }
        public int RatingCount { get; set; }
        public double? AverageRating { get; set; }

        // Location data
        public string? State { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? LocationName { get; set; }

        // Category-specific data as a nested dictionary
        public Dictionary<string, object> CategoryData { get; set; } = new Dictionary<string, object>();
        public DateTime ListedDate { get; internal set; }

        /// <summary>
        /// Converts a domain Item model to a FirebaseItem
        /// </summary>
        public static FirebaseItem FromItem(Item item, string id = "")
        {
            var firebaseItem = new FirebaseItem
            {
                Id = string.IsNullOrEmpty(id) ? GenerateId() : id,
                Title = item.Title,
                Description = item.Description,
                Price = item.Price,
                Category = item.Category.ToString(),
                Status = item.Status.ToString(),
                PrimaryPhotoUrl = item.GetMainPhotoUrl(),
                PostedByUserId = item.PostedByUserId.ToString(),
                Version = item.Version,
                LastModified = DateTime.UtcNow
            };

            // Add statistics if available
            firebaseItem.ViewCount = item.ViewCount;
            firebaseItem.InquiryCount = item.InquiryCount;
            firebaseItem.FavoriteCount = item.FavoriteCount;
            firebaseItem.RatingCount = item.RatingCount;
            firebaseItem.AverageRating = item.AverageRating;

            // Add location if available
            if (item.Latitude.HasValue)
                firebaseItem.Latitude = item.Latitude;

            if (item.Longitude.HasValue)
                firebaseItem.Longitude = item.Longitude;

            if (item.ItemLocation != null)
                firebaseItem.LocationName = item.ItemLocation.LocationName;

            if (item.State.HasValue)
                firebaseItem.State = item.State.Value.ToString();

            // Add category-specific details based on item type
            switch (item.Category)
            {
                case ItemCategory.ForSale:
                    if (item.ForSaleCategory.HasValue)
                        firebaseItem.CategoryData["subcategory"] = item.ForSaleCategory.Value.ToString();
                    if (!string.IsNullOrEmpty(item.Condition))
                        firebaseItem.CategoryData["condition"] = item.Condition;
                    firebaseItem.CategoryData["isNegotiable"] = item.IsNegotiable;
                    break;

                case ItemCategory.ForRent:
                    if (item.ForRentCategory.HasValue)
                        firebaseItem.CategoryData["subcategory"] = item.ForRentCategory.Value.ToString();
                    if (!string.IsNullOrEmpty(item.RentalPeriod))
                        firebaseItem.CategoryData["rentalPeriod"] = item.RentalPeriod;

                    if (item.AvailableFrom.HasValue)
                        firebaseItem.CategoryData["availableFrom"] = item.AvailableFrom.Value.ToString("o");

                    if (item.AvailableTo.HasValue)
                        firebaseItem.CategoryData["availableTo"] = item.AvailableTo.Value.ToString("o");
                    break;

                case ItemCategory.Job:
                    if (item.JobCategory.HasValue)
                        firebaseItem.CategoryData["subcategory"] = item.JobCategory.Value.ToString();
                    if (!string.IsNullOrEmpty(item.JobType))
                        firebaseItem.CategoryData["jobType"] = item.JobType;
                    if (!string.IsNullOrEmpty(item.CompanyName))
                        firebaseItem.CategoryData["companyName"] = item.CompanyName;
                    if (!string.IsNullOrEmpty(item.JobLocation))
                        firebaseItem.CategoryData["jobLocation"] = item.JobLocation;

                    if (item.ApplyMethod.HasValue)
                        firebaseItem.CategoryData["applyMethod"] = item.ApplyMethod.Value.ToString();

                    if (!string.IsNullOrEmpty(item.ApplyContact))
                        firebaseItem.CategoryData["applyContact"] = item.ApplyContact;

                    firebaseItem.CategoryData["isSalaryDisclosed"] = item.IsSalaryDisclosed;
                    break;

                case ItemCategory.Service:
                    if (item.ServiceCategory.HasValue)
                        firebaseItem.CategoryData["subcategory"] = item.ServiceCategory.Value.ToString();

                    if (item.ServiceAvailability.HasValue)
                        firebaseItem.CategoryData["availability"] = item.ServiceAvailability.Value.ToString();

                    if (item.YearsOfExperience.HasValue)
                        firebaseItem.CategoryData["yearsOfExperience"] = item.YearsOfExperience.Value;

                    if (item.NumberOfEmployees.HasValue)
                        firebaseItem.CategoryData["numberOfEmployees"] = item.NumberOfEmployees.Value;

                    if (!string.IsNullOrEmpty(item.ServiceType))
                        firebaseItem.CategoryData["serviceType"] = item.ServiceType;

                    if (!string.IsNullOrEmpty(item.ServiceLocation))
                        firebaseItem.CategoryData["serviceLocation"] = item.ServiceLocation;
                    break;
            }

            return firebaseItem;
        }

        /// <summary>
        /// Converts back to a domain Item model
        /// </summary>
        public Item ToDomainModel()
        {
            // Parse enums
            var category = Enum.TryParse<ItemCategory>(this.Category, true, out var parsedCategory)
                ? parsedCategory : ItemCategory.ForSale;
           
            var status = Enum.TryParse<ItemStatus>(this.Status, out var parsedStatus)
                ? parsedStatus : ItemStatus.Active;

            // Try to parse numeric user ID
            string userId = string.Empty; // Initialize as string
            if (int.TryParse(this.PostedByUserId, out var parsedId)) // Use int.TryParse
                userId = parsedId.ToString(); // Convert back to string if needed

            // Create the base item
            var item = new Item
            {
                Title = this.Title,
                Description = this.Description,
                Price = this.Price,
                Category = category,
                Status = status,
                ListedDate = this.CreatedAt,
                PhotoUrl = this.PrimaryPhotoUrl,
                ViewCount = this.ViewCount,
                InquiryCount = this.InquiryCount,
                FavoriteCount = this.FavoriteCount,
                RatingCount = this.RatingCount,
                AverageRating = this.AverageRating,
                Version = this.Version,
                PostedByUserId = userId,
                PostedByUser = null
            };

            // Try to parse numeric ID for compatibility
            if (int.TryParse(this.Id, out var numericId)) // Use int.TryParse
                item.Id = numericId.ToString(); // Convert back to string if needed

            // Add location data if available
            if (this.Latitude.HasValue && this.Longitude.HasValue)
            {
                item.Latitude = this.Latitude;
                item.Longitude = this.Longitude;

                // Create ItemLocation if needed
                item.ItemLocation = new ItemLocation
                {
                    ItemId = item.Id,
                    Latitude = this.Latitude.Value,
                    Longitude = this.Longitude.Value,
                    LocationName = this.LocationName
                };
            }

            // Parse state if present
            if (!string.IsNullOrEmpty(this.State) && Enum.TryParse<AlState>(this.State, out var state))
                item.State = state;

            // Add category-specific details based on category
            switch (category)
            {
                case ItemCategory.ForSale:
                    if (CategoryData.TryGetValue("subcategory", out var forSaleCatObj) &&
                        Enum.TryParse<ForSaleCategory>(forSaleCatObj.ToString(), out var forSaleCat))
                        item.ForSaleCategory = forSaleCat;

                    if (CategoryData.TryGetValue("condition", out var condition))
                        item.Condition = condition?.ToString();

                    if (CategoryData.TryGetValue("isNegotiable", out var isNegotiableObj) &&
                        bool.TryParse(isNegotiableObj.ToString(), out var isNegotiable))
                        item.IsNegotiable = isNegotiable;
                    break;

                case ItemCategory.ForRent:
                    if (CategoryData.TryGetValue("subcategory", out var forRentCatObj) &&
                        Enum.TryParse<ForRentCategory>(forRentCatObj.ToString(), out var forRentCat))
                        item.ForRentCategory = forRentCat;

                    if (CategoryData.TryGetValue("rentalPeriod", out var rentalPeriod))
                        item.RentalPeriod = rentalPeriod?.ToString();

                    if (CategoryData.TryGetValue("availableFrom", out var availableFromObj) &&
                        DateTime.TryParse(availableFromObj.ToString(), out var availableFrom))
                        item.AvailableFrom = availableFrom;

                    if (CategoryData.TryGetValue("availableTo", out var availableToObj) &&
                        DateTime.TryParse(availableToObj.ToString(), out var availableTo))
                        item.AvailableTo = availableTo;
                    break;

                case ItemCategory.Job:
                    if (CategoryData.TryGetValue("subcategory", out var jobCatObj) &&
                        Enum.TryParse<JobCategory>(jobCatObj.ToString(), out var jobCat))
                        item.JobCategory = jobCat;

                    if (CategoryData.TryGetValue("jobType", out var jobType))
                        item.JobType = jobType?.ToString();

                    if (CategoryData.TryGetValue("companyName", out var companyName))
                        item.CompanyName = companyName?.ToString();

                    if (CategoryData.TryGetValue("jobLocation", out var jobLocation))
                        item.JobLocation = jobLocation?.ToString();

                    if (CategoryData.TryGetValue("applyMethod", out var applyMethodObj) &&
                        Enum.TryParse<ApplyMethod>(applyMethodObj.ToString(), out var applyMethod))
                        item.ApplyMethod = applyMethod;

                    if (CategoryData.TryGetValue("applyContact", out var applyContact))
                        item.ApplyContact = applyContact?.ToString();

                    if (CategoryData.TryGetValue("isSalaryDisclosed", out var isSalaryDisclosedObj) &&
                        bool.TryParse(isSalaryDisclosedObj.ToString(), out var isSalaryDisclosed))
                        item.IsSalaryDisclosed = isSalaryDisclosed;
                    break;

                case ItemCategory.Service:
                    if (CategoryData.TryGetValue("subcategory", out var serviceCatObj) &&
                        Enum.TryParse<ServiceCategory>(serviceCatObj.ToString(), out var serviceCat))
                        item.ServiceCategory = serviceCat;

                    if (CategoryData.TryGetValue("availability", out var serviceAvailObj) &&
                        Enum.TryParse<ServiceAvailability>(serviceAvailObj.ToString(), out var serviceAvail))
                        item.ServiceAvailability = serviceAvail;

                    if (CategoryData.TryGetValue("yearsOfExperience", out var yearsExpObj) &&
              int.TryParse(yearsExpObj.ToString(), out var yearsExp)) // Use int.TryParse
                        item.YearsOfExperience = yearsExp;

                    if (CategoryData.TryGetValue("numberOfEmployees", out var numEmployeesObj) &&
                int.TryParse(numEmployeesObj.ToString(), out var numEmployees)) // Use int.TryParse
                        item.NumberOfEmployees = numEmployees;

                    if (CategoryData.TryGetValue("serviceType", out var serviceType))
                        item.ServiceType = serviceType?.ToString();

                    if (CategoryData.TryGetValue("serviceLocation", out var serviceLocation))
                        item.ServiceLocation = serviceLocation?.ToString();
                    break;
            }

            return item;
        }

        /// <summary>
        /// Creates index entries for this item for efficient Firebase queries
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var indexUpdates = new Dictionary<string, object>();

            // User-Item index
            indexUpdates[$"user_items/{PostedByUserId}/{Id}"] = new Dictionary<string, object>
            {
                ["timestamp"] = CreatedTimestamp,
                ["type"] = "Posted"
            };

            // Category index
            indexUpdates[$"items_by_category/{Category}/{Id}"] = true;

            // Status index
            indexUpdates[$"items_by_status/{Status}/{Id}"] = CreatedTimestamp;

            // State-Item index (if state is provided)
            if (!string.IsNullOrEmpty(State))
            {
                indexUpdates[$"items_by_state/{State}/{Id}"] = true;
            }

            // Geolocation index (if coordinates are provided)
            if (Latitude.HasValue && Longitude.HasValue)
            {
                // Generate a geohash (simplified version)
                var geohash = GenerateGeohash(Latitude.Value, Longitude.Value, 6);
                indexUpdates[$"items_by_location/{geohash}/{Id}"] = new Dictionary<string, object>
                {
                    ["latitude"] = Latitude.Value,
                    ["longitude"] = Longitude.Value
                };
            }

            // Category-specific indexes
            if (CategoryData.TryGetValue("subcategory", out var subcategory))
            {
                switch (Category)
                {
                    case "ForSale":
                        indexUpdates[$"items_by_sale_category/{subcategory}/{Id}"] = true;
                        break;
                    case "ForRent":
                        indexUpdates[$"items_by_rent_category/{subcategory}/{Id}"] = true;
                        break;
                    case "Job":
                        indexUpdates[$"items_by_job_category/{subcategory}/{Id}"] = true;
                        break;
                    case "Service":
                        indexUpdates[$"items_by_service_category/{subcategory}/{Id}"] = true;
                        break;
                }
            }

            return indexUpdates;
        }

        /// <summary>
        /// Converts to a Firebase-compatible dictionary
        /// </summary>
        public override Dictionary<string, object> ToFirebaseObject()
        {
            var result = base.ToFirebaseObject();

            result["title"] = Title;
            result["description"] = Description;
            result["price"] = Price;
            result["category"] = Category;
            result["status"] = Status;
            result["postedByUserId"] = PostedByUserId;
            result["version"] = Version;
            result["lastModified"] = LastModified;

            if (!string.IsNullOrEmpty(PrimaryPhotoUrl))
                result["primaryPhotoUrl"] = PrimaryPhotoUrl;

            result["viewCount"] = ViewCount;
            result["inquiryCount"] = InquiryCount;
            result["favoriteCount"] = FavoriteCount;
            result["ratingCount"] = RatingCount;

            if (AverageRating.HasValue)
                result["averageRating"] = AverageRating.Value;

            if (!string.IsNullOrEmpty(State))
                result["state"] = State;

            if (Latitude.HasValue)
                result["latitude"] = Latitude.Value;

            if (Longitude.HasValue)
                result["longitude"] = Longitude.Value;

            if (!string.IsNullOrEmpty(LocationName))
                result["locationName"] = LocationName;

            if (CategoryData.Count > 0)
                result["categoryDetails"] = CategoryData;

            return result;
        }

        /// <summary>
        /// Generate a geohash from latitude and longitude
        /// </summary>
        private static string GenerateGeohash(double latitude, double longitude, int precision)
        {
            const string base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
            double[] lat = { -90.0, 90.0 };
            double[] lon = { -180.0, 180.0 };
            char[] geohash = new char[precision];
            bool isEven = true;

            int bit = 0, ch = 0;
            int hashIndex = 0;

            while (hashIndex < precision)
            {
                double mid;
                if (isEven)
                {
                    mid = (lon[0] + lon[1]) / 2;
                    if (longitude > mid)
                    {
                        ch |= (1 << (4 - bit));
                        lon[0] = mid;
                    }
                    else
                    {
                        lon[1] = mid;
                    }
                }
                else
                {
                    mid = (lat[0] + lat[1]) / 2;
                    if (latitude > mid)
                    {
                        ch |= (1 << (4 - bit));
                        lat[0] = mid;
                    }
                    else
                    {
                        lat[1] = mid;
                    }
                }

                isEven = !isEven;
                if (bit < 4)
                {
                    bit++;
                }
                else
                {
                    geohash[hashIndex] = base32[ch];
                    hashIndex++;
                    bit = 0;
                    ch = 0;
                }
            }

            return new string(geohash);
        }
    }
}