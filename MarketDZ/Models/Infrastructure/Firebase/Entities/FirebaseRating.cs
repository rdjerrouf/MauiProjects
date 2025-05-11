
using Google.Apis.Util.Store;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Firebase-specific implementation of the Rating model
    /// </summary>
    public class FirebaseRating : FirebaseEntity
    {
        /// <summary>
        /// The user who submitted the rating
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The item being rated
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// Rating score (typically 1-5)
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Optional written review
        /// </summary>
        public string Review { get; set; } = string.Empty;

        /// <summary>
        /// Whether this was a verified purchase
        /// </summary>
        public bool IsVerifiedPurchase { get; set; }

        /// <summary>
        /// Number of helpful votes this rating received
        /// </summary>
        public int HelpfulVotes { get; set; }

        /// <summary>
        /// When this rating was last updated
        /// </summary>
        public long UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Creates a FirebaseRating from a Rating domain model
        /// </summary>
        public static FirebaseRating FromRating(Rating rating, string id = "")
        {
            // If no ID is provided, use a composite key of itemId_userId
            var ratingId = string.IsNullOrEmpty(id)
                ? $"{rating.ItemId}_{rating.UserId}"
                : id;

            var result = new FirebaseRating
            {
                Id = ratingId,
                UserId = rating.UserId.ToString(),
                ItemId = rating.ItemId.ToString(),
                Score = rating.Score,
                Review = rating.Review ?? string.Empty,
                IsVerifiedPurchase = rating.IsVerifiedPurchase,
                HelpfulVotes = rating.HelpfulVotes ?? 0,
                CreatedTimestamp = rating.CreatedAt.ToUniversalTime().Ticks / 10000 // Convert to milliseconds
            };

            return result;
        }

        /// <summary>
        /// Converts back to a Rating domain model
        /// </summary>
        public Rating ToRating()
        {
            var rating = new Rating
            {
                Score = this.Score,
                Review = this.Review,
                CreatedAt = this.CreatedAt,
                IsVerifiedPurchase = this.IsVerifiedPurchase,
                HelpfulVotes = this.HelpfulVotes
            };

            // Convert string IDs to integers for compatibility
            if (int.TryParse(this.Id, out int ratingId))
                rating.Id = ratingId.ToString(); // Fix: Convert int to string
            if (int.TryParse(this.UserId, out int userId))
                rating.UserId = userId.ToString(); // Fix: Convert int to string
            if (int.TryParse(this.ItemId, out int itemId))
                rating.ItemId = itemId.ToString(); // Fix: Convert int to string

            return rating;
        }

        /// <summary>
        /// Creates index entries for this rating
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var updates = new Dictionary<string, object>();

            // Index by user
            updates[$"user_ratings/{UserId}/{ItemId}"] = new Dictionary<string, object>
            {
                ["score"] = Score,
                ["createdTimestamp"] = CreatedTimestamp
            };

            // Index by item
            updates[$"item_ratings/{ItemId}/{UserId}"] = new Dictionary<string, object>
            {
                ["score"] = Score,
                ["createdTimestamp"] = CreatedTimestamp
            };

            return updates;
        }

        /// <summary>
        /// Creates stats update entries for recalculating item stats
        /// </summary>
        public async Task<Dictionary<string, object>> CreateStatsUpdateEntriesAsync(IAppCoreDataStore dataStore)
        {
            var updates = new Dictionary<string, object>();

            // Get all ratings for this item to recalculate the average
            var itemRatings = await dataStore.GetCollectionAsync<FirebaseRating>($"item_ratings/{ItemId}");

            var count = itemRatings.Count;
            var average = count > 0
                ? Math.Round(itemRatings.Average(r => r.Score), 1)
                : 0;

            // Update the item stats
            updates[$"items/{ItemId}/ratingCount"] = count;
            updates[$"items/{ItemId}/averageRating"] = average;

            return updates;
        }

        /// <summary>
        /// Converts to a Firebase-compatible dictionary
        /// </summary>
        public override Dictionary<string, object> ToFirebaseObject()
        {
            var result = base.ToFirebaseObject();

            result["userId"] = UserId;
            result["itemId"] = ItemId;
            result["score"] = Score;
            result["updatedTimestamp"] = UpdatedTimestamp;

            if (!string.IsNullOrEmpty(Review))
                result["review"] = Review;

            result["isVerifiedPurchase"] = IsVerifiedPurchase;
            result["helpfulVotes"] = HelpfulVotes;

            return result;
        }
    }
}
