using MarketDZ.Models;
using System.Text;
using MarketDZ.Models.Filters;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    public static class CacheExtensions
    {
        /// <summary>
        /// Generates a consistent cache key for items
        /// </summary>
        public static string GetItemCacheKey(string itemId) => $"item:{itemId}";

        /// <summary>
        /// Generates a cache key for user items
        /// </summary>
        public static string GetUserItemsCacheKey(string userId) => $"user_items:{userId}";

        /// <summary>
        /// Generates a cache key for category items
        /// </summary>
        public static string GetCategoryItemsCacheKey(string category) => $"category_items:{category}";

        /// <summary>
        /// Generates a cache key for item photos
        /// </summary>
        public static string GetItemPhotosCacheKey(string itemId) => $"item_photos:{itemId}";

        /// <summary>
        /// Generates a cache key for user favorites
        /// </summary>
        public static string GetUserFavoritesCacheKey(string userId) => $"user_favorites:{userId}";

        /// <summary>
        /// Generates a cache key for item ratings
        /// </summary>
        public static string GetItemRatingsCacheKey(string itemId) => $"item_ratings:{itemId}";

        /// <summary>
        /// Generates a cache key for search results
        /// </summary>
        public static string GetSearchCacheKey(FilterParameters filter) =>
            $"search:{filter.GetCacheKey()}";

        /// <summary>
        /// Determines the cache policy based on the type of data
        /// </summary>
        public static CachePolicy DetermineCachePolicy(string key)
        {
            if (key.StartsWith("category_items:") || key.StartsWith("state_items:"))
                return CachePolicy.Stable;

            if (key.StartsWith("item:") || key.StartsWith("user_items:"))
                return CachePolicy.Moderate;

            if (key.StartsWith("search:") || key.Contains("viewCount") || key.Contains("inquiryCount"))
                return CachePolicy.Volatile;

            if (key.Contains("category:list") || key.Contains("state:list"))
                return CachePolicy.Immutable;

            return CachePolicy.Moderate;
        }

        /// <summary>
        /// Gets dependencies for a cache item
        /// </summary>
        public static IEnumerable<string> GetCacheDependencies(string key)
        {
            var dependencies = new List<string>();

            if (key.StartsWith("item:"))
            {
                // Item depends on user
                string itemId = key.Split(':')[1];
                if (!string.IsNullOrEmpty(itemId))
                {
                    dependencies.Add($"user:item_owner:{itemId}");
                }
            }
            else if (key.StartsWith("user_items:"))
            {
                // User items depend on user
                string userId = key.Split(':')[1];
                if (!string.IsNullOrEmpty(userId))
                {
                    dependencies.Add($"user:{userId}");
                }
            }
            else if (key.StartsWith("item_photos:"))
            {
                // Item photos depend on item
                string itemId = key.Split(':')[1];
                if (!string.IsNullOrEmpty(itemId))
                {
                    dependencies.Add($"item:{itemId}");
                }
            }
            else if (key.StartsWith("item_ratings:"))
            {
                // Item ratings depend on item
                string itemId = key.Split(':')[1];
                if (!string.IsNullOrEmpty(itemId))
                {
                    dependencies.Add($"item:{itemId}");
                }
            }

            return dependencies;
        }

        public static string GetCacheKey(this FilterParameters filter)
        {
            if (filter == null) return "null";

            var keyBuilder = new StringBuilder();

            // Add filter properties to key
            if (!string.IsNullOrEmpty(filter.SearchText))
                keyBuilder.Append($"search:{filter.SearchText};");

            // Add more properties as needed
            if (filter.Page > 0)
                keyBuilder.Append($"page:{filter.Page};");

            // Using proper type conversion for PageSize which is declared as object
            if (filter.PageSize != null && Convert.ToInt32(filter.PageSize) > 0)
                keyBuilder.Append($"pageSize:{filter.PageSize};");

            // Add other properties from FilterParameters

            return keyBuilder.ToString();
        }

        // Add this extension method if not already there
        public static string GetCacheKey(this FilterCriteria criteria)
        {
            // You can reuse your existing GenerateCacheKey method logic here
            if (criteria == null) return "null";

            var keyBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(criteria.SearchText))
                keyBuilder.Append($"search:{criteria.SearchText};");

            // Add the rest of the properties from your GenerateCacheKey method

            return keyBuilder.ToString();
        }


    }
}