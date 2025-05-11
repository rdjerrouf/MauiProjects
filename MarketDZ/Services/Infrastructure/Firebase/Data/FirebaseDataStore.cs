using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Firebase.Database;
using Firebase.Database.Query;
using MarketDZ.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MarketDZ.Services.Infrastructure.Firebase.Data
{
    /// <summary>
    /// Firebase implementation of IAppCoreDataStore
    /// </summary>
    public class FirebaseDataStore : IAppCoreDataStore
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly HttpClient _httpClient;
        private readonly string _firebaseUrl;
        private readonly ICacheService _cacheService;
        private readonly ILogger<FirebaseDataStore> _logger;
        private readonly FirebaseQueryConverter _queryConverter;
        protected readonly int _maxRetries = 3;
        protected readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(1);
        private bool _isInitialized;
        private static SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _maintenanceCts = new CancellationTokenSource();

        /// <summary>
        /// Creates a new Firebase data store
        /// </summary>
        /// <param name="cacheService">Cache service</param>
        /// <param name="logger">Logger</param>
        /// <param name="firebaseUrl">Firebase URL (optional)</param>
        public FirebaseDataStore(
            ICacheService cacheService,
            ILogger<FirebaseDataStore> logger,
            string? firebaseUrl = null)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Store the Firebase URL for later use
            _firebaseUrl = firebaseUrl ?? "https://marketdz-a6db7-default-rtdb.firebaseio.com/";

            // Log the Firebase URL we're trying to connect to
            _logger.LogInformation($"Initializing Firebase with URL: {_firebaseUrl}");

            // Initialize Firebase client
            _firebaseClient = new FirebaseClient(
                _firebaseUrl,
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = () => Task.FromResult<string>("AIzaSyC3MJJ7XtyS6mEIkYWUUQ9o_HkHQ77QQcg")
                });
            // Initialize HttpClient for direct API access
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Initialize query converter
            _queryConverter = new FirebaseQueryConverter(logger);

            // Start background cache maintenance
            _ = _cacheService.StartMaintenanceAsync(_maintenanceCts.Token);
        }

        /// <summary>
        /// Initializes the data store
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                _logger.LogInformation("Starting Firebase initialization");

                // Create a proper JSON object to send
                var testData = new { message = "Connection test: " + DateTime.UtcNow.ToString() };

                // Put the data as a properly formatted JSON object
                await _firebaseClient
                    .Child("test")
                    .PutAsync(testData);

                _logger.LogInformation("Firebase write test successful");

                // Try reading data as well
                var readResult = await _firebaseClient
                    .Child("test")
                    .OnceSingleAsync<object>();

                _logger.LogInformation($"Firebase read test result: {readResult != null}");
                _logger.LogInformation("Firebase connection successful");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Firebase initialization error");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }


        private Task<string> GetAuthTokenAsync()
        {
            try
            {
                string apiKey = "AIzaSyC3MJJ7XtyS6mEIkYWUUQ9o_HkHQ77QQcg";
                _logger.LogInformation("Using API key authentication for Firebase. Note this needs to change after testing");
                return Task.FromResult(apiKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get auth token");
                return Task.FromResult(string.Empty);
            }
        }
        /// <summary>
        /// Retrieves a single entity by its path
        /// </summary>
        public async Task<T> GetEntityAsync<T>(string path)
        {
            await InitializeAsync();

            string cacheKey = $"get:{path}";
            if (_cacheService.TryGetFromCache<T>(cacheKey, out var cachedItem))
            {
                _logger.LogInformation($"Cache hit for {cacheKey}");
                return cachedItem!;
            }

            _logger.LogInformation($"Fetching data from {path}");
            var result = await ExecuteWithRetryAsync(() =>
                _firebaseClient.Child(path).OnceSingleAsync<T>(),
                $"GetEntityAsync:{path}");

            if (result != null)
            {
                _cacheService.AddToCache(cacheKey, result);
            }

            return result;
        }

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        public async Task<IReadOnlyCollection<T>> GetCollectionAsync<T>(string path, IQueryParameters? parameters = null)
        {
            await InitializeAsync();

            string cacheKey = $"collection:{path}" + (parameters != null ? ":" + parameters.GetCacheKey() : "");
            if (_cacheService.TryGetFromCache<List<T>>(cacheKey, out var cachedItems))
            {
                _logger.LogInformation($"Cache hit for {cacheKey}");
                return cachedItems!;
            }

            _logger.LogInformation($"Fetching collection data from {path}");

            try
            {
                // Build the query using the converter
                ChildQuery query = parameters != null
                    ? _queryConverter.BuildQuery(_firebaseClient, path, parameters)
                    : _firebaseClient.Child(path);

                // Get raw JSON first to handle both array and object formats properly
                var jsonResponse = await query.OnceAsJsonAsync();

                if (string.IsNullOrWhiteSpace(jsonResponse) || jsonResponse == "null")
                {
                    _logger.LogWarning($"Empty or null response from {path}");
                    return new List<T>();
                }

                List<T> result;

                // Check if the response is an array
                if (jsonResponse.TrimStart().StartsWith("["))
                {
                    _logger.LogInformation("Data is in array format, processing as array");

                    // Process as array
                    var jArray = JArray.Parse(jsonResponse);
                    result = new List<T>();

                    for (int i = 0; i < jArray.Count; i++)
                    {
                        if (jArray[i] != null && jArray[i].Type != JTokenType.Null)
                        {
                            try
                            {
                                var item = jArray[i].ToObject<T>();
                                if (item != null)
                                {
                                    result.Add(item);
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                _logger.LogError(jsonEx, $"Error deserializing item at index {i}");
                            }
                        }
                    }

                    _logger.LogInformation($"Retrieved {result.Count} items via array processing from {path}");
                }
                else
                {
                    // Process as object
                    try
                    {
                        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, T>>(jsonResponse);
                        if (dictionary != null)
                        {
                            result = dictionary
                                .Where(kvp => kvp.Value != null)
                                .Select(kvp => kvp.Value)
                                .ToList();

                            _logger.LogInformation($"Retrieved {result.Count} items via dictionary processing from {path}");
                        }
                        else
                        {
                            result = new List<T>();
                            _logger.LogWarning("Deserialized dictionary is null");
                        }
                    }
                    catch (JsonException dictEx)
                    {
                        _logger.LogError(dictEx, "Error deserializing as dictionary: {Message}", dictEx.Message);
                        result = new List<T>();
                    }
                }

                // Apply any client-side filters if we have parameters
                if (parameters != null)
                {
                    result = _queryConverter.ApplyClientSideFilters(result, parameters).ToList();
                }

                // Cache the results
                _cacheService.AddToCache(cacheKey, result);

                return result;
            }
            catch (FirebaseException fireEx)
            {
                _logger.LogError(fireEx, $"Firebase error in GetCollectionAsync for path {path}: {fireEx.Message}");
                return new List<T>();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, $"JSON deserialization error in GetCollectionAsync for path {path}: {jsonEx.Message}");
                return new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetCollectionAsync for path {path}: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Creates or updates an entity at the specified path
        /// </summary>
        public async Task<T> SetEntityAsync<T>(string path, T data)
        {
            await InitializeAsync();

            // Validate data
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Cannot set null data to Firebase");
            }

            _logger.LogInformation($"Setting data at {path}");

            // Execute the operation with retries
            await ExecuteWithRetryAsync<object>(() =>
                _firebaseClient.Child(path).PutAsync<T>(data).ContinueWith(t => (object)new object()),
                $"SetEntityAsync:{path}");

            // Invalidate cache for this path and related collection paths
            _cacheService.InvalidateCache($"get:{path}");
            string collectionPath = path.Split('/')[0];
            _cacheService.InvalidateCachePattern($"collection:{collectionPath}");

            return data;
        }

        /// <summary>
        /// Adds a new entity to a collection with an auto-generated key
        /// </summary>
        public async Task<(string Key, T Entity)> AddEntityAsync<T>(string path, T data)
        {
            await InitializeAsync();

            // Validate data
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data), "Cannot add null data to Firebase");
            }

            _logger.LogInformation($"Adding new item to {path}");

            var result = await ExecuteWithRetryAsync(() =>
                _firebaseClient.Child(path).PostAsync(data),
                $"AddEntityAsync:{path}");

            // Invalidate collection cache
            _cacheService.InvalidateCachePattern($"collection:{path}");

            return (result.Key, result.Object);
        }

        /// <summary>
        /// Updates specific fields of an entity
        /// </summary>
        public async Task UpdateEntityFieldsAsync(string path, IDictionary<string, object> updates)
        {
            await InitializeAsync();

            // Validate updates
            if (updates == null || !updates.Any())
            {
                throw new ArgumentException("Updates dictionary cannot be null or empty", nameof(updates));
            }

            _logger.LogInformation($"Updating properties at {path}: {string.Join(", ", updates.Keys)}");

            foreach (var update in updates)
            {
                await ExecuteWithRetryAsync<object>(() =>
                    _firebaseClient.Child(path).Child(update.Key).PutAsync<object>(update.Value).ContinueWith(t => (object)null),
                    $"UpdateEntityFieldsAsync:{path}/{update.Key}");
            }

            // Invalidate related caches
            _cacheService.InvalidateCache($"get:{path}");
            string collectionPath = path.Split('/')[0];
            _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
        }

        /// <summary>
        /// Deletes an entity at the specified path
        /// </summary>
        public async Task DeleteEntityAsync(string path)
        {
            await InitializeAsync();

            _logger.LogInformation($"Deleting data at {path}");

            await ExecuteWithRetryAsync<object>(() =>
            _firebaseClient.Child(path).DeleteAsync().ContinueWith(t => (object)new object()),
                $"DeleteEntityAsync:{path}");

            // Invalidate related caches
            _cacheService.InvalidateCache($"get:{path}");
            string collectionPath = path.Split('/')[0];
            _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
        }

        /// <summary>
        /// Performs multiple update operations in a batch
        /// </summary>
        public async Task BatchUpdateAsync(Dictionary<string, object> updates)
        {
            await InitializeAsync();

            try
            {
                var tasks = updates.Select(kvp =>
                    _firebaseClient.Child(kvp.Key).PutAsync(kvp.Value));

                await Task.WhenAll(tasks);

                // Invalidate affected cache entries
                foreach (var path in updates.Keys)
                {
                    _cacheService.InvalidateCache($"get:{path}");
                    string collectionPath = path.Split('/')[0];
                    _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing batch update");
                throw;
            }
        }

        /// <summary>
        /// Performs multiple delete operations in a batch
        /// </summary>
        public async Task BatchDeleteAsync(IEnumerable<string> paths)
        {
            await InitializeAsync();

            try
            {
                var tasks = paths.Select(path =>
                    _firebaseClient.Child(path).DeleteAsync());

                await Task.WhenAll(tasks);

                // Invalidate affected cache entries
                foreach (var path in paths)
                {
                    _cacheService.InvalidateCache($"get:{path}");
                    string collectionPath = path.Split('/')[0];
                    _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing batch delete");
                throw;
            }
        }

        /// <summary>
        /// Gets the number of items in a collection
        /// </summary>
        public async Task<int> GetCollectionSizeAsync(string path)
        {
            await InitializeAsync();

            string cacheKey = $"size:{path}";
            if (_cacheService.TryGetFromCache<int>(cacheKey, out var cachedSize))
            {
                _logger.LogInformation($"Cache hit for {cacheKey}");
                return cachedSize;
            }

            _logger.LogInformation($"Counting items in collection at {path}");

            try
            {
                var items = await GetCollectionAsync<object>(path);
                int size = items.Count;

                // Cache the size
                _cacheService.AddToCache(cacheKey, size);

                return size;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting collection size for path {path}");
                return 0;
            }
        }

        /// <summary>
        /// Begins a transaction for atomic operations
        /// </summary>
        public Task<ITransaction> BeginTransactionAsync()
        {
            // Create a transaction without using _loggerFactory
            return Task.FromResult<ITransaction>(new FirebaseTransaction(_firebaseClient, this, _logger));
        }

        /// <summary>
        /// Execute a Firebase operation with retries
        /// </summary>
        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    attempt++;
                    return await operation();
                }
                catch (Exception ex)
                {
                    if (attempt >= _maxRetries)
                    {
                        _logger.LogError(ex, $"Operation '{operationName}' failed after {attempt} attempts");
                        throw;
                    }

                    _logger.LogInformation($"Attempt {attempt} for operation '{operationName}' failed, retrying in {_retryDelay.TotalSeconds} seconds");
                    await Task.Delay(_retryDelay);
                }
            }
        }

        /// <summary>
        /// Disposes the data store
        /// </summary>
        public void Dispose()
        {
            _maintenanceCts?.Cancel();
            _maintenanceCts?.Dispose();
            _httpClient?.Dispose();
        }

        /// <summary>
        /// Retrieves a filtered collection of entities
        /// </summary>
        public async Task<List<T>> GetFilteredCollectionAsync<T>(string path, IQueryParameters parameters)
        {
            await InitializeAsync();

            string cacheKey = $"filteredCollection:{path}" + (parameters != null ? ":" + parameters.GetCacheKey() : "");
            if (_cacheService.TryGetFromCache<List<T>>(cacheKey, out var cachedItems))
            {
                _logger.LogInformation($"Cache hit for {cacheKey}");
                return cachedItems!;
            }

            _logger.LogInformation($"Fetching filtered collection data from {path}");

            try
            {
                // Build the query using the converter
                ChildQuery query = _queryConverter.BuildQuery(_firebaseClient, path, parameters);

                // Get raw JSON first to handle both array and object formats
                var jsonResponse = await query.OnceAsJsonAsync();

                if (string.IsNullOrWhiteSpace(jsonResponse) || jsonResponse == "null")
                {
                    return new List<T>();
                }

                List<T> result;

                // Check if the response is an array
                if (jsonResponse.TrimStart().StartsWith("["))
                {
                    // Process as array
                    var jArray = JArray.Parse(jsonResponse);
                    result = new List<T>();

                    for (int i = 0; i < jArray.Count; i++)
                    {
                        if (jArray[i] != null && jArray[i].Type != JTokenType.Null)
                        {
                            try
                            {
                                var item = jArray[i].ToObject<T>();
                                if (item != null)
                                {
                                    result.Add(item);
                                }
                            }
                            catch (JsonException jsonEx)
                            {
                                _logger.LogError(jsonEx, $"Error deserializing item at index {i}");
                            }
                        }
                    }
                }
                else
                {
                    // Process as object
                    try
                    {
                        var dictionary = JsonConvert.DeserializeObject<Dictionary<string, T>>(jsonResponse);
                        result = dictionary?
                            .Where(kvp => kvp.Value != null)
                            .Select(kvp => kvp.Value)
                            .ToList() ?? new List<T>();
                    }
                    catch (JsonException dictEx)
                    {
                        _logger.LogError(dictEx, "Error deserializing as dictionary");
                        result = new List<T>();
                    }
                }

                // Apply any remaining client-side filters
                result = _queryConverter.ApplyClientSideFilters(result, parameters).ToList();

                _logger.LogInformation($"Retrieved {result.Count} items via filtered query from {path}");

                // Cache the results
                _cacheService.AddToCache(cacheKey, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error in GetFilteredCollectionAsync for path {path}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Gets a document by its path, similar to GetEntityAsync but with a different name
        /// </summary>
        public async Task<T> GetDocumentAsync<T>(string path)
        {
            // This is essentially the same as GetEntityAsync
            return await GetEntityAsync<T>(path);
        }
        public Task DeleteAsync<T>(string key)
        {
            // Assuming DeleteEntityAsync exists and handles deletions
            return DeleteEntityAsync(key);
        }

        public Task StoreAsync(string key, object value)
        {
            // Assuming SetEntityAsync exists and handles storing entities
            return SetEntityAsync(key, value);
        }

        /// <summary>
        /// Updates an entity in the database
        /// </summary>
        public async Task UpdateEntityAsync(string path, Conversation conversation)
        {
            await InitializeAsync();

            if (conversation == null)
            {
                throw new ArgumentNullException(nameof(conversation));
            }

            _logger.LogInformation($"Updating entity at {path}");

            // Convert Conversation to FirebaseConversation
            var firebaseConversation = FirebaseConversation.FromConversation(conversation);

            // Increment version
            firebaseConversation.Version = conversation.Version + 1;
            firebaseConversation.LastModified = DateTime.UtcNow;

            // Execute with retry
            await ExecuteWithRetryAsync<object>(() =>
                _firebaseClient.Child(path).PutAsync(firebaseConversation).ContinueWith(t => (object)null),
                $"UpdateEntityAsync:{path}");

            // Invalidate cache
            _cacheService.InvalidateCache($"get:{path}");
            string collectionPath = path.Split('/')[0];
            _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
        }

        /// <summary>
        /// Updates specific fields of an entity
        /// </summary>
        public async Task UpdateEntityFieldsAsync(string path, Conversation conversation)
        {
            await InitializeAsync();

            if (conversation == null)
            {
                throw new ArgumentNullException(nameof(conversation));
            }

            _logger.LogInformation($"Updating fields for entity at {path}");

            // Create a dictionary of fields to update
            var updates = new Dictionary<string, object>
            {
                ["title"] = conversation.Title ?? string.Empty,
                ["lastMessagePreview"] = conversation.LastMessagePreview ?? string.Empty,
                ["lastMessageAt"] = conversation.LastMessageAt,
                ["lastMessageSenderId"] = conversation.LastMessageSenderId,
                // Fix for CS0019: Ensure the types in the null-coalescing operator match.
                // Change the default value to match the type of `UnreadCountPerUser` which is `Dictionary<string, int>`.

                ["unreadCountPerUser"] = conversation.UnreadCountPerUser ?? new Dictionary<string, int>(),
                // Fix for CS8601: Possible null reference assignment.
                // Ensure that null values are replaced with a default value to avoid null reference issues.

                ["lastMessageSenderId"] = conversation.LastMessageSenderId ?? string.Empty,
                // Fix for CS8601: Ensure the types in the null-coalescing operator match.
                // Change the default value to match the type of `UnreadCountPerUser` which is `Dictionary<string, int>`.

                ["lastMessageSenderId"] = conversation.LastMessageSenderId ?? string.Empty,
                ["unreadCountPerUser"] = conversation.UnreadCountPerUser ?? new Dictionary<string, int>(),
                ["unreadCountPerUser"] = conversation.UnreadCountPerUser ?? new Dictionary<string, int>(),
                ["isArchived"] = conversation.IsArchived,
                ["version"] = conversation.Version + 1,
                ["lastModified"] = DateTime.UtcNow
            };

            // Execute individual field updates with retry
            var tasks = updates.Select(update =>
                ExecuteWithRetryAsync<object>(() =>
                    _firebaseClient.Child(path).Child(update.Key).PutAsync(update.Value).ContinueWith(t => (object)null),
                    $"UpdateEntityFieldsAsync:{path}/{update.Key}")
            );

            await Task.WhenAll(tasks);

            // Invalidate related caches
            _cacheService.InvalidateCache($"get:{path}");
            string collectionPath = path.Split('/')[0];
            _cacheService.InvalidateCachePattern($"collection:{collectionPath}");
        }

        /// <summary>
        /// Helper method to migrate array-structured data to use proper Firebase keys
        /// </summary>
        public async Task MigrateArrayToKeyValueStructureAsync<T>(string collectionPath)
        {
            _logger.LogInformation($"Starting migration of {collectionPath} from array to key-value structure");

            try
            {
                // Get current array data
                var jsonResponse = await _firebaseClient.Child(collectionPath).OnceAsJsonAsync();

                if (string.IsNullOrWhiteSpace(jsonResponse) || jsonResponse == "null" || !jsonResponse.TrimStart().StartsWith("["))
                {
                    _logger.LogInformation($"No array found at {collectionPath}, nothing to migrate");
                    return;
                }

                // Parse array
                var jArray = JArray.Parse(jsonResponse);
                var migrationTasks = new List<Task>();

                // For each non-null item in the array
                for (int i = 0; i < jArray.Count; i++)
                {
                    if (jArray[i] == null || jArray[i].Type == JTokenType.Null)
                        continue;

                    try
                    {
                        // Create a new entry with a push ID
                        var item = jArray[i].ToObject<T>();
                        if (item != null)
                        {
                            // Use PostAsync to get a new push ID
                            migrationTasks.Add(_firebaseClient.Child(collectionPath).PostAsync(item));
                        }
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, $"Error migrating item at index {i}");
                    }
                }

                // Wait for all migrations to complete
                await Task.WhenAll(migrationTasks);

                // Now delete the old array (potentially dangerous, consider backing up first)
                // await _firebaseClient.Child(collectionPath).DeleteAsync();

                _logger.LogInformation($"Migration of {collectionPath} completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during migration of {collectionPath}");
                throw;
            }
        }
    }
}