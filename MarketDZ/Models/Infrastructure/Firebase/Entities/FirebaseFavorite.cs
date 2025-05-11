using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    public class FirebaseFavorite : FirebaseEntity
    {
        /// <summary>
        /// The user ID who favorited the item
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The item ID that was favorited
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// When this favorite was added
        /// </summary>
        public long DateAddedTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Converts DateAddedTimestamp to DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime DateAdded => DateTimeOffset.FromUnixTimeMilliseconds(DateAddedTimestamp).DateTime;

        /// <summary>
        /// Creates a FirebaseFavorite from a UserFavorite domain model
        /// </summary>
        public static FirebaseFavorite FromUserFavorite(UserFavorite favorite, string id = "")
        {
            var result = new FirebaseFavorite
            {
                Id = string.IsNullOrEmpty(id) ? GenerateId() : id,
                UserId = favorite.UserId.ToString(),
                ItemId = favorite.ItemId.ToString(),
                DateAddedTimestamp = favorite.DateAdded.ToUniversalTime().Ticks / 10000 // Convert to milliseconds
            };

            return result;
        }

        /// <summary>
        /// Converts back to a UserFavorite domain model
        /// </summary>
        public UserFavorite ToUserFavorite()
        {
            var favorite = new UserFavorite
            {
                DateAdded = this.DateAdded
            };

            // Convert string IDs to integers for compatibility
            if (!string.IsNullOrEmpty(this.UserId))
                favorite.UserId = this.UserId;

            if (!string.IsNullOrEmpty(this.ItemId))
                favorite.ItemId = this.ItemId;

            return favorite;
        }

        /// <summary>
        /// Creates index entries for this favorite
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var updates = new Dictionary<string, object>();

            // User's favorites index
            updates[$"user_favorites/{UserId}/{ItemId}"] = DateAddedTimestamp;

            // Item's favorited-by index
            updates[$"item_favorites/{ItemId}/{UserId}"] = DateAddedTimestamp;

            return updates;
        }

        /// <summary>
        /// Creates removal entries for this favorite (for deleting)
        /// </summary>
        public Dictionary<string, object> CreateRemovalEntries()
        {
            var updates = new Dictionary<string, object>();

            // User's favorites index
            updates[$"user_favorites/{UserId}/{ItemId}"] = null;

            // Item's favorited-by index
            updates[$"item_favorites/{ItemId}/{UserId}"] = null;

            // The favorite itself
            updates[$"favorites/{Id}"] = null;

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
            result["dateAddedTimestamp"] = DateAddedTimestamp;

            return result;
        }
    }
}
