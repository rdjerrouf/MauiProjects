using System.Diagnostics;

namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    public static class IdConversionHelper
    {
        private const string IdMappingPath = "id_mappings";
        private const string CounterPath = "id_counters";

        /// <summary>
        /// Converts any ID to a Firebase push key
        /// </summary>
        public static async Task<string> ConvertToFirebaseIdAsync(IAppCoreDataStore dataStore, string id, string entityType)
        {
            // If the ID is already in Firebase format (non-numeric or empty), return it directly
            if (string.IsNullOrEmpty(id) || !int.TryParse(id, out int numericId) || numericId <= 0)
                return id ?? GeneratePushId();

            // Check if we already have a mapping for the numeric ID
            var existingMapping = await dataStore.GetEntityAsync<string>($"{IdMappingPath}/{entityType}/numeric/{numericId}");
            if (!string.IsNullOrEmpty(existingMapping))
                return existingMapping;

            // Create new mapping
            string firebaseId = GeneratePushId();
            await BatchUpdateAsync(dataStore, new Dictionary<string, object>
            {
                [$"{IdMappingPath}/{entityType}/numeric/{numericId}"] = firebaseId,
                [$"{IdMappingPath}/{entityType}/firebase/{firebaseId}"] = numericId.ToString()
            });
            return firebaseId;
        }

        /// <summary>
        /// Converts a Firebase push key to a numeric ID (as string)
        /// </summary>
        public static async Task<string> ConvertToNumericIdAsync(IAppCoreDataStore dataStore, string firebaseId, string entityType)
        {
            if (string.IsNullOrEmpty(firebaseId))
                return string.Empty;

            // If the ID is already numeric, return it directly
            if (int.TryParse(firebaseId, out _))
                return firebaseId;

            // Check if we have a mapping
            var numericIdStr = await dataStore.GetEntityAsync<string>($"{IdMappingPath}/{entityType}/firebase/{firebaseId}");
            if (!string.IsNullOrEmpty(numericIdStr))
                return numericIdStr;

            // If no mapping exists, create a new numeric ID
            int newId = await GetNextCounterAsync(dataStore, entityType);

            // Store the mapping
            await BatchUpdateAsync(dataStore, new Dictionary<string, object>
            {
                [$"{IdMappingPath}/{entityType}/numeric/{newId}"] = firebaseId,
                [$"{IdMappingPath}/{entityType}/firebase/{firebaseId}"] = newId.ToString()
            });
            return newId.ToString();
        }

        /// <summary>
        /// Generates a new sequential numeric ID for a given entity type
        /// </summary>
        public static async Task<int> GetNextCounterAsync(IAppCoreDataStore dataStore, string entityType)
        {
            var counterPath = $"{CounterPath}/{entityType}";
            var currentValue = await dataStore.GetEntityAsync<int>(counterPath);
            int newValue = currentValue + 1;

            await dataStore.SetEntityAsync(counterPath, newValue);
            return newValue;
        }

        /// <summary>
        /// Generates a Firebase push ID
        /// </summary>
        public static string GeneratePushId()
        {
            // Firebase-style push ID generator
            var timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds;
            var random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            var randomPart = new string(Enumerable.Repeat(chars, 12)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            return $"{timestamp:x8}-{randomPart}";
        }

        /// <summary>
        /// Migrates existing data to use the new ID system
        /// </summary>
        public static async Task MigrateDataToNewIdSystem<T>(
            IAppCoreDataStore dataStore,
            string collectionPath,
            Func<T, string> idExtractor,
            Action<T, string> newIdSetter) where T : class
        {
            try
            {
                // Get all existing entities
                var entities = await dataStore.GetCollectionAsync<T>(collectionPath);
                if (entities == null || !entities.Any())
                    return;

                var updates = new Dictionary<string, object>();

                foreach (var entity in entities)
                {
                    var existingId = idExtractor(entity);

                    // Skip if not a numeric ID
                    if (!int.TryParse(existingId, out int legacyId) || legacyId <= 0)
                        continue;

                    // Generate new ID and update mapping
                    string newId = await ConvertToFirebaseIdAsync(dataStore, existingId, typeof(T).Name);

                    // Update the entity with the new ID
                    newIdSetter(entity, newId);

                    // Add to batch update
                    updates[$"{collectionPath}/{newId}"] = entity;
                    updates[$"{collectionPath}/{existingId}"] = null; // Remove old entry
                }

                if (updates.Any())
                {
                    await BatchUpdateAsync(dataStore, updates);
                }
            }
            catch (Exception ex)
            {
                // Log error and continue
                Debug.WriteLine($"Migration error: {ex.Message}");
            }
        }

        /// <summary>
        /// Helper method to perform batch updates
        /// </summary>
        private static async Task BatchUpdateAsync(IAppCoreDataStore dataStore, Dictionary<string, object> updates)
        {
            foreach (var update in updates)
            {
                if (update.Value == null)
                {
                    await dataStore.DeleteAsync<object>(update.Key);
                }
                else
                {
                    await dataStore.StoreAsync(update.Key, update.Value);
                }
            }
        }
    }
}