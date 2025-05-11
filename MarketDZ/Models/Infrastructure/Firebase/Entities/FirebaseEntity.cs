using Newtonsoft.Json;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Base class for all Firebase entities, providing common properties
    /// and methods for Firebase compatibility
    /// </summary>
    public abstract class FirebaseEntity
    {
        /// <summary>
        /// Unique identifier (Firebase key)
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Creation timestamp
        /// </summary>
        public long CreatedTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Last updated timestamp
        /// </summary>
        public long UpdatedTimestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        /// <summary>
        /// Converts CreatedTimestamp to DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime CreatedAt => DateTimeOffset.FromUnixTimeMilliseconds(CreatedTimestamp).DateTime;

        /// <summary>
        /// Converts UpdatedTimestamp to DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime UpdatedAt => DateTimeOffset.FromUnixTimeMilliseconds(UpdatedTimestamp).DateTime;

        /// <summary>
        /// Generates a new Firebase-compatible ID
        /// </summary>
        public static string GenerateId()
        {
            // Create a timestamp-based ID similar to Firebase's push IDs
            // Format: -MMMMMMMMMM-NNNNNNNNNN where M is timestamp and N is random
            var timestamp = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7FFFFFFFFL).ToString("X8");
            var random = new Random().Next(0, 0x7FFFFFFF).ToString("X8");
            return $"-{timestamp}-{random}";
        }

        /// <summary>
        /// Updates the entity's timestamps before saving
        /// </summary>
        public virtual void PrepareForSave()
        {
            // If this is a new entity, generate an ID
            if (string.IsNullOrEmpty(Id))
            {
                Id = GenerateId();
            }

            // Always update the UpdatedTimestamp
            UpdatedTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Converts the entity to a Firebase-compatible dictionary
        /// </summary>
        public virtual Dictionary<string, object> ToFirebaseObject()
        {
            return new Dictionary<string, object>
            {
                ["id"] = Id,
                ["createdTimestamp"] = CreatedTimestamp,
                ["updatedTimestamp"] = UpdatedTimestamp
            };
        }
    }
}