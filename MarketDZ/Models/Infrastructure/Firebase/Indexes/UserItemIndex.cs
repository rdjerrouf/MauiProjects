using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Models.Infrastructure.Firebase.Indexes
{
    /// <summary>
    /// User-to-Item index entry for user's items and favorites
    /// </summary>
    public class UserItemIndex : FirebaseIndex
    {
        /// <summary>
        /// The user ID
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// The item ID
        /// </summary>
        public string ItemId { get; set; } = string.Empty;

        /// <summary>
        /// When this relationship was created
        /// </summary>
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Optional relationship type (Posted, Favorited, etc.)
        /// </summary>
        public string RelationType { get; set; } = string.Empty;

        /// <summary>
        /// Creates a new user-item index entry
        /// </summary>
        public UserItemIndex() { }

        /// <summary>
        /// Creates a new user-item index entry
        /// </summary>
        public UserItemIndex(string userId, string itemId, string relationType = "")
        {
            UserId = userId;
            ItemId = itemId;
            TargetId = itemId; // Set TargetId to ItemId for standard base class behavior
            RelationType = relationType;
        }

        /// <summary>
        /// Generates the path for this index in Firebase
        /// </summary>
        public string GetIndexPath()
        {
            return $"user_items/{UserId}/{ItemId}";
        }

        /// <summary>
        /// Converts to a Firebase-compatible value
        /// </summary>
        public override object ToFirebaseValue()
        {
            return new Dictionary<string, object>
            {
                ["timestamp"] = Timestamp,
                ["type"] = RelationType
            };
        }
    }
}
