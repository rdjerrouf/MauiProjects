using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Firebase-specific implementation of the User model
    /// </summary>
    public class FirebaseUser : FirebaseEntity
    {
        /// <summary>
        /// User's email address
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Hashed password (not stored in client memory)
        /// </summary>
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// User's display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// URL to profile picture
        /// </summary>
        public string ProfilePicture { get; set; } = string.Empty;

        /// <summary>
        /// User's bio
        /// </summary>
        public string Bio { get; set; } = string.Empty;

        /// <summary>
        /// User's phone number
        /// </summary>
        public string PhoneNumber { get; set; } = string.Empty;

        /// <summary>
        /// User's city
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// User's province/state
        /// </summary>
        public string Province { get; set; } = string.Empty;

        /// <summary>
        /// Flag indicating whether to show email publicly
        /// </summary>
        public bool ShowEmail { get; set; }

        /// <summary>
        /// Flag indicating whether to show phone number publicly
        /// </summary>
        public bool ShowPhoneNumber { get; set; }

        /// <summary>
        /// Flag indicating whether the email is verified
        /// </summary>
        public bool IsEmailVerified { get; set; }

        /// <summary>
        /// When the email was verified
        /// </summary>
        public long? EmailVerifiedTimestamp { get; set; }

        /// <summary>
        /// Flag indicating whether the user is an admin
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Count of items posted by this user
        /// </summary>
        public int PostedItemsCount { get; set; }

        /// <summary>
        /// Count of items favorited by this user
        /// </summary>
        public int FavoriteItemsCount { get; set; }

        /// <summary>
        /// Converts EmailVerifiedTimestamp to DateTime
        /// </summary>
        [JsonIgnore]
        public DateTime? EmailVerifiedAt => EmailVerifiedTimestamp.HasValue
            ? DateTimeOffset.FromUnixTimeMilliseconds(EmailVerifiedTimestamp.Value).DateTime
            : null;

        /// <summary>
        /// Creates a FirebaseUser from a User domain model
        /// </summary>
        public static FirebaseUser FromUser(User user, string id = "")
        {
            var result = new FirebaseUser
            {
                Id = string.IsNullOrEmpty(id) ? (user.Id.ToString()) : id,
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                DisplayName = user.DisplayName ?? string.Empty,
                ProfilePicture = user.ProfilePicture ?? string.Empty,
                Bio = user.Bio ?? string.Empty,
                PhoneNumber = user.PhoneNumber ?? string.Empty,
                City = user.City ?? string.Empty,
                Province = user.Province ?? string.Empty,
                ShowEmail = user.ShowEmail,
                ShowPhoneNumber = user.ShowPhoneNumber,
                IsEmailVerified = user.IsEmailVerified,
                IsAdmin = user.IsAdmin,
                CreatedTimestamp = user.CreatedAt.ToUniversalTime().Ticks / 10000 // Convert to milliseconds
            };

            if (user.EmailVerifiedAt.HasValue)
            {
                result.EmailVerifiedTimestamp = user.EmailVerifiedAt.Value.ToUniversalTime().Ticks / 10000;
            }

            // Calculate counts if possible
            result.PostedItemsCount = user.PostedItems?.Count ?? 0;
            result.FavoriteItemsCount = user.FavoriteItems?.Count ?? 0;

            return result;
        }

        /// <summary>
        /// Converts back to a User domain model
        /// </summary>
        public User ToUser()
        {
            var user = new User
            {
                Email = this.Email,
                PasswordHash = this.PasswordHash,
                DisplayName = this.DisplayName,
                ProfilePicture = this.ProfilePicture,
                Bio = this.Bio,
                CreatedAt = this.CreatedAt,
                PhoneNumber = this.PhoneNumber,
                City = this.City,
                Province = this.Province,
                ShowEmail = this.ShowEmail,
                ShowPhoneNumber = this.ShowPhoneNumber,
                IsEmailVerified = this.IsEmailVerified,
                EmailVerifiedAt = this.EmailVerifiedAt,
                IsAdmin = this.IsAdmin
            };

            // Convert string ID to integer for compatibility
            if (!string.IsNullOrEmpty(this.Id))
            {
                user.Id = this.Id;
            }

            return user;
        }

        /// <summary>
        /// Creates security-sensitive profile for public viewing
        /// </summary>
        public Dictionary<string, object> ToPublicProfile()
        {
            var profile = new Dictionary<string, object>
            {
                ["id"] = Id,
                ["displayName"] = DisplayName,
                ["profilePicture"] = ProfilePicture,
                ["bio"] = Bio,
                ["createdTimestamp"] = CreatedTimestamp,
                ["city"] = City,
                ["province"] = Province,
                ["postedItemsCount"] = PostedItemsCount,
                ["favoriteItemsCount"] = FavoriteItemsCount
            };

            // Only include email if the user has opted to show it
            if (ShowEmail)
            {
                profile["email"] = Email;
            }

            // Only include phone if the user has opted to show it
            if (ShowPhoneNumber)
            {
                profile["phoneNumber"] = PhoneNumber;
            }

            return profile;
        }

        /// <summary>
        /// Creates index entries for this user
        /// </summary>
        public Dictionary<string, object> CreateIndexEntries()
        {
            var updates = new Dictionary<string, object>();

            // Email index for quick lookup
            updates[$"users_by_email/{NormalizeEmail(Email)}"] = Id;

            return updates;
        }

        /// <summary>
        /// Normalizes an email address for consistent lookups
        /// </summary>
        private string NormalizeEmail(string email)
        {
            return email.ToLowerInvariant().Replace(".", "_dot_").Replace("@", "_at_");
        }

        /// <summary>
        /// Converts to a Firebase-compatible dictionary
        /// </summary>
        public override Dictionary<string, object> ToFirebaseObject()
        {
            var result = base.ToFirebaseObject();

            result["email"] = Email;
            result["passwordHash"] = PasswordHash;

            if (!string.IsNullOrEmpty(DisplayName))
                result["displayName"] = DisplayName;

            if (!string.IsNullOrEmpty(ProfilePicture))
                result["profilePicture"] = ProfilePicture;

            if (!string.IsNullOrEmpty(Bio))
                result["bio"] = Bio;

            if (!string.IsNullOrEmpty(PhoneNumber))
                result["phoneNumber"] = PhoneNumber;

            if (!string.IsNullOrEmpty(City))
                result["city"] = City;

            if (!string.IsNullOrEmpty(Province))
                result["province"] = Province;

            result["showEmail"] = ShowEmail;
            result["showPhoneNumber"] = ShowPhoneNumber;
            result["isEmailVerified"] = IsEmailVerified;

            if (EmailVerifiedTimestamp.HasValue)
                result["emailVerifiedTimestamp"] = EmailVerifiedTimestamp.Value;

            result["isAdmin"] = IsAdmin;
            result["postedItemsCount"] = PostedItemsCount;
            result["favoriteItemsCount"] = FavoriteItemsCount;

            return result;
        }
    }
}