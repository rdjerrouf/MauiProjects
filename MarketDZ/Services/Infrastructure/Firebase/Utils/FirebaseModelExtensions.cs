using MarketDZ.Converters;
using MarketDZ.Models.Firebase.Base;
using MarketDZ.Models.Firebase;
using Newtonsoft.Json;

namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    /// <summary>
    /// Extension methods for working with Firebase models
    /// </summary>
    public static class FirebaseModelExtensions
    {
        /// <summary>
        /// Ensures a property value is compatible with Firebase
        /// </summary>
        public static object ToFirebaseValue(this object value)
        {
            if (value == null)
                return null;

            // Handle enums as strings
            if (value.GetType().IsEnum)
                return value.ToString();

            // Handle DateTime as ISO strings
            if (value is DateTime dateTime)
                return dateTime.ToString("o");

            // Handle collections as arrays
            if (value is ICollection<object> collection)
                return collection.Select(v => ToFirebaseValue(v)).ToArray();

            // Pass through primitive types
            if (value is string || value is bool || value is int || value is long ||
                value is double || value is float || value is decimal)
                return value;

            // Convert complex objects to dictionaries
            if (value is FirebaseEntity entity)
                return entity.ToFirebaseObject();

            // Default to JSON serialization for complex objects
            return JsonConvert.SerializeObject(value);
        }

        /// <summary>
        /// Creates a sanitized key safe for Firebase paths
        /// </summary>
        public static string ToFirebaseKey(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            // Replace illegal characters
            return text
                .Replace(".", "_dot_")
                .Replace("$", "_dollar_")
                .Replace("#", "_hash_")
                .Replace("[", "_lbrack_")
                .Replace("]", "_rbrack_")
                .Replace("/", "_slash_");
        }

        /// <summary>
        /// Normalizes an email address for Firebase indexing
        /// </summary>
        public static string NormalizeEmail(this string email)
        {
            if (string.IsNullOrEmpty(email))
                return string.Empty;

            return email.ToLowerInvariant()
                .Replace(".", "_dot_")
                .Replace("@", "_at_");
        }

        /// <summary>
        /// Converts a collection of Firebase models to their domain models
        /// </summary>
        public static List<T> ToDomainModels<T, TFirebase>(this IEnumerable<TFirebase> firebaseModels)
            where TFirebase : FirebaseEntity, IDomainModelConverter<T>
            where T : class
        {
            if (firebaseModels == null)
                return new List<T>();

            return firebaseModels
                .Where(fb => fb != null)
                .Select(fb => fb.ToDomainModel())
                .Where(model => model != null)
                .ToList();
        }

        /// <summary>
        /// Converts a collection of domain models to their Firebase models
        /// </summary>
        public static List<TFirebase> ToFirebaseModels<T, TFirebase>(this IEnumerable<T> domainModels, Func<T, TFirebase> converter)
            where TFirebase : FirebaseEntity
            where T : class
        {
            if (domainModels == null)
                return new List<TFirebase>();

            return domainModels
                .Where(model => model != null)
                .Select(converter)
                .Where(fb => fb != null)
                .ToList();
        }
    

  

    }
}