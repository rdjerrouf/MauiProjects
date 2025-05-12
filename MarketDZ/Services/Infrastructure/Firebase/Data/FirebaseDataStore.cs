using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Services.Core.Interfaces.Data;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace MarketDZ.Services.Infrastructure.Firebase.Data
{
    /// <summary>
    /// Firebase implementation of the IAppCoreDataStore interface
    /// </summary>
    public class FirebaseDataStore : IAppCoreDataStore
    {
        private readonly string _databaseUrl;
        private readonly FirebaseClient _firebaseClient;
        private readonly ILogger<FirebaseDataStore> _logger;
        private bool _initialized = false;

        public FirebaseDataStore(
            string databaseUrl,
            FirebaseClient firebaseClient,
            ILogger<FirebaseDataStore> logger)
        {
            _databaseUrl = databaseUrl ?? throw new ArgumentNullException(nameof(databaseUrl));
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Initializes the connection to Firebase
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                _logger.LogInformation("Initializing Firebase connection to {DatabaseUrl}", _databaseUrl);

                // Attempt a test connection
                await _firebaseClient.GetAsync("");

                _initialized = true;
                _logger.LogInformation("Firebase connection initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Firebase connection: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets an entity by its path
        /// </summary>
        public async Task<T> GetEntityAsync<T>(string path) where T : class
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Getting entity at path: {Path}", path);

                var response = await _firebaseClient.GetAsync(path);

                if (string.IsNullOrEmpty(response) || response == "null")
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<T>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity at path {Path}: {Message}", path, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Gets a collection of entities
        /// </summary>
        public async Task<IReadOnlyCollection<T>> GetCollectionAsync<T>(string path, IQueryParameters parameters = null) where T : class
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Getting collection at path: {Path}", path);

                string queryPath = path;

                // Apply query parameters if provided
                if (parameters != null)
                {
                    // Convert parameters to Firebase query string
                    var queryParts = new List<string>();

                    // Add ordering
                    if (parameters.SortCriteria.Any())
                    {
                        var sortCriterion = parameters.SortCriteria.First();
                        string orderBy = $"orderBy=\"{sortCriterion.Field}\"";
                        queryParts.Add(orderBy);

                        if (sortCriterion.Direction == Core.Models.SortDirection.Descending)
                        {
                            queryParts.Add("limitToLast=true");
                        }
                    }

                    // Add filters (basic Firebase REST query support is limited)
                    foreach (var filter in parameters.FilterCriteria)
                    {
                        switch (filter.Operator)
                        {
                            case FilterOperator.Equal:
                                queryParts.Add($"equalTo=\"{JsonConvert.SerializeObject(filter.Value).Trim('"')}\"");
                                break;
                            case FilterOperator.LessThan:
                                queryParts.Add($"endAt=\"{JsonConvert.SerializeObject(filter.Value).Trim('"')}\"");
                                break;
                            case FilterOperator.LessThanOrEqual:
                                queryParts.Add($"endAt=\"{JsonConvert.SerializeObject(filter.Value).Trim('"')}\"");
                                break;
                            case FilterOperator.GreaterThan:
                                queryParts.Add($"startAt=\"{JsonConvert.SerializeObject(filter.Value).Trim('"')}\"");
                                break;
                            case FilterOperator.GreaterThanOrEqual:
                                queryParts.Add($"startAt=\"{JsonConvert.SerializeObject(filter.Value).Trim('"')}\"");
                                break;
                            default:
                                _logger.LogWarning("Unsupported filter operator: {Operator}", filter.Operator);
                                break;
                        }
                    }

                    // Add pagination
                    if (parameters.Take > 0)
                    {
                        queryParts.Add($"limitToFirst={parameters.Take}");
                    }

                    // Build query string
                    if (queryParts.Any())
                    {
                        queryPath += "?" + string.Join("&", queryParts);
                    }
                }

                var response = await _firebaseClient.GetAsync(queryPath);

                if (string.IsNullOrEmpty(response) || response == "null")
                {
                    return new List<T>().AsReadOnly();
                }

                // Parse Firebase response (key-value dictionary)
                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, T>>(response);

                if (dictionary == null || !dictionary.Any())
                {
                    return new List<T>().AsReadOnly();
                }

                // Apply client-side pagination if needed (Firebase REST API has limited pagination support)
                var result = dictionary.Values.ToList();

                if (parameters != null && parameters.Skip > 0)
                {
                    result = result.Skip(parameters.Skip).ToList();
                }

                return result.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection at path {Path}: {Message}", path, ex.Message);
                return new List<T>().AsReadOnly();
            }
        }

        /// <summary>
        /// Creates or updates an entity
        /// </summary>
        public async Task<T> SetEntityAsync<T>(string path, T data) where T : class
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Setting entity at path: {Path}", path);

                var json = JsonConvert.SerializeObject(data);
                await _firebaseClient.PutAsync(path, json);

                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting entity at path {Path}: {Message}", path, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Adds a new entity to a collection with an auto-generated ID
        /// </summary>
        public async Task<(string Key, T Entity)> AddEntityAsync<T>(string path, T data) where T : class
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Adding entity to collection: {Path}", path);

                var json = JsonConvert.SerializeObject(data);
                var result = await _firebaseClient.PostAsync(path, json);

                // Extract the generated name/key from Firebase response
                var response = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
                string key = response["name"];

                return (key, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding entity to collection {Path}: {Message}", path, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Updates specific fields of an entity
        /// </summary>
        public async Task UpdateEntityFieldsAsync(string path, IDictionary<string, object> updates)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Updating fields at path: {Path}", path);

                var json = JsonConvert.SerializeObject(updates);
                await _firebaseClient.PatchAsync(path, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating fields at path {Path}: {Message}", path, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Deletes an entity
        /// </summary>
        public async Task DeleteEntityAsync(string path)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Deleting entity at path: {Path}", path);

                await _firebaseClient.DeleteAsync(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting entity at path {Path}: {Message}", path, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Performs multiple update operations in a single batch
        /// </summary>
        public async Task BatchUpdateAsync(Dictionary<string, object> updates)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            if (updates == null || !updates.Any())
            {
                return;
            }

            try
            {
                _logger.LogTrace("Performing batch update with {Count} operations", updates.Count);

                // Firebase REST API doesn't support true batching, so we use a transaction
                using var transaction = await BeginTransactionAsync();

                foreach (var update in updates)
                {
                    if (update.Value == null)
                    {
                        await transaction.DeleteEntityAsync(update.Key);
                    }
                    else
                    {
                        await transaction.SetEntityAsync(update.Key, update.Value);
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing batch update: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Performs multiple delete operations in a single batch
        /// </summary>
        public async Task BatchDeleteAsync(IEnumerable<string> paths)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            if (paths == null || !paths.Any())
            {
                return;
            }

            try
            {
                _logger.LogTrace("Performing batch delete with {Count} operations", paths.Count());

                // Convert paths to a batch update with null values (Firebase way to delete)
                var updates = paths.ToDictionary(path => path, path => (object)null);
                await BatchUpdateAsync(updates);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing batch delete: {Message}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// Gets the number of items in a collection
        /// </summary>
        public async Task<int> GetCollectionSizeAsync(string path)
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            try
            {
                _logger.LogTrace("Getting collection size at path: {Path}", path);

                // Firebase doesn't have a direct way to get collection size
                // We'll fetch just the keys to minimize data transfer
                var response = await _firebaseClient.GetAsync($"{path}?shallow=true");

                if (string.IsNullOrEmpty(response) || response == "null")
                {
                    return 0;
                }

                var dictionary = JsonConvert.DeserializeObject<Dictionary<string, bool>>(response);
                return dictionary?.Count ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collection size at path {Path}: {Message}", path, ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Begins a transaction for atomic operations
        /// </summary>
        public async Task<ITransaction> BeginTransactionAsync()
        {
            if (!_initialized)
            {
                await InitializeAsync();
            }

            return new FirebaseTransaction(_firebaseClient, _logger);
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Dispose()
        {
            // Firebase client doesn't need explicit disposal
        }
    }

    /// <summary>
    /// Firebase implementation of the ITransaction interface
    /// </summary>
    public class FirebaseTransaction : ITransaction
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly ILogger _logger;
        private readonly Dictionary<string, object> _updates = new Dictionary<string, object>();
        private readonly HashSet<string> _deletions = new HashSet<string>();
        private readonly Dictionary<string, object> _readCache = new Dictionary<string, object>();
        private TransactionStatus _status = TransactionStatus.Active;

        public FirebaseTransaction(
            FirebaseClient firebaseClient,
            ILogger logger)
        {
            _firebaseClient = firebaseClient ?? throw new ArgumentNullException(nameof(firebaseClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current transaction status
        /// </summary>
        public TransactionStatus Status => _status;

        /// <summary>
        /// Gets an entity within the transaction
        /// </summary>
        public async Task<T> GetEntityAsync<T>(string path) where T : class
        {
            CheckActive();

            // Check if entity was already read in this transaction
            if (_readCache.TryGetValue(path, out var cached) && cached is T cachedEntity)
            {
                return cachedEntity;
            }

            // Check if entity was updated in this transaction
            if (_updates.TryGetValue(path, out var updated) && updated is T updatedEntity)
            {
                _readCache[path] = updatedEntity;
                return updatedEntity;
            }

            // Check if entity was deleted in this transaction
            if (_deletions.Contains(path))
            {
                return null;
            }

            try
            {
                var response = await _firebaseClient.GetAsync(path);

                if (string.IsNullOrEmpty(response) || response == "null")
                {
                    return null;
                }

                var entity = JsonConvert.DeserializeObject<T>(response);
                _readCache[path] = entity;

                return entity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entity in transaction at path {Path}: {Message}", path, ex.Message);
                _status = TransactionStatus.Failed;
                throw;
            }
        }

        /// <summary>
        /// Creates or updates an entity within the transaction
        /// </summary>
        public Task SetEntityAsync<T>(string path, T data) where T : class
        {
            CheckActive();

            _updates[path] = data;
            _deletions.Remove(path); // In case it was scheduled for deletion

            return Task.CompletedTask;
        }

        /// <summary>
        /// Updates specific fields of an entity within the transaction
        /// </summary>
        public async Task UpdateFieldsAsync(string path, IDictionary<string, object> updates)
        {
            CheckActive();

            // Get current entity
            var currentEntity = await GetEntityAsync<Dictionary<string, object>>(path);

            if (currentEntity == null)
            {
                currentEntity = new Dictionary<string, object>();
            }

            // Apply updates
            foreach (var update in updates)
            {
                currentEntity[update.Key] = update.Value;
            }

            // Store updated entity
            _updates[path] = currentEntity;
            _deletions.Remove(path); // In case it was scheduled for deletion
        }

        /// <summary>
        /// Deletes an entity within the transaction
        /// </summary>
        public Task DeleteEntityAsync(string path)
        {
            CheckActive();

            _deletions.Add(path);
            _updates.Remove(path); // In case it was scheduled for update

            return Task.CompletedTask;
        }

        /// <summary>
        /// Commits all changes in the transaction
        /// </summary>
        public async Task CommitAsync()
        {
            CheckActive();

            try
            {
                if (!_updates.Any() && !_deletions.Any())
                {
                    _status = TransactionStatus.Committed;
                    return;
                }

                // Create a batch update payload
                var batch = new Dictionary<string, object>();

                // Add updates
                foreach (var update in _updates)
                {
                    batch[update.Key] = update.Value;
                }

                // Add deletions as null values
                foreach (var path in _deletions)
                {
                    batch[path] = null;
                }

                // Execute batch update using Firebase's PATCH method
                var json = JsonConvert.SerializeObject(batch);
                await _firebaseClient.PatchAsync("", json);

                _status = TransactionStatus.Committed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error committing transaction: {Message}", ex.Message);
                _status = TransactionStatus.Failed;
                throw;
            }
        }

        /// <summary>
        /// Rolls back all changes in the transaction
        /// </summary>
        public Task RollbackAsync()
        {
            CheckActive();

            _updates.Clear();
            _deletions.Clear();
            _status = TransactionStatus.RolledBack;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Checks if the transaction is active
        /// </summary>
        private void CheckActive()
        {
            if (_status != TransactionStatus.Active)
            {
                throw new InvalidOperationException($"Transaction is not active. Current status: {_status}");
            }
        }

        /// <summary>
        /// Clean up resources
        /// </summary>
        public void Dispose()
        {
            if (_status == TransactionStatus.Active)
            {
                try
                {
                    RollbackAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error rolling back transaction during disposal: {Message}", ex.Message);
                }
            }
        }
    }

    /// <summary>
    /// Simple HTTP client for Firebase Realtime Database REST API
    /// </summary>
    public class FirebaseClient
    {
        private readonly string _baseUrl;
        private readonly string _authToken;
        private readonly HttpClient _httpClient;

        public FirebaseClient(string databaseUrl, string authToken = null)
        {
            if (string.IsNullOrEmpty(databaseUrl))
            {
                throw new ArgumentNullException(nameof(databaseUrl));
            }

            _baseUrl = databaseUrl.TrimEnd('/');
            _authToken = authToken;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Builds a URL with optional auth token
        /// </summary>
        private string BuildUrl(string path)
        {
            string url = $"{_baseUrl}/{path}.json";

            if (!string.IsNullOrEmpty(_authToken))
            {
                url += $"?auth={_authToken}";
            }

            return url;
        }

        /// <summary>
        /// Gets data from Firebase
        /// </summary>
        public async Task<string> GetAsync(string path)
        {
            var url = BuildUrl(path);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Creates or replaces data at the specified path
        /// </summary>
        public async Task<string> PutAsync(string path, string json)
        {
            var url = BuildUrl(path);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(url, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Creates a new child with an auto-generated key
        /// </summary>
        public async Task<string> PostAsync(string path, string json)
        {
            var url = BuildUrl(path);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Updates specific fields at the specified path
        /// </summary>
        public async Task<string> PatchAsync(string path, string json)
        {
            var url = BuildUrl(path);
            var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Deletes data at the specified path
        /// </summary>
        public async Task<string> DeleteAsync(string path)
        {
            var url = BuildUrl(path);
            var response = await _httpClient.DeleteAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
    }
}