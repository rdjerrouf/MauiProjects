using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Models.Filters;
using Microsoft.Extensions.Logging;
using MarketDZ.Services.Core.Interfaces.Data;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Services.Infrastructure.Firebase.Utils;
using MarketDZ.Models.Infrastructure.Firebase.Entities;



namespace MarketDZ.Services.Infrastructure.Firebase.Repositories
{
    /// <summary>
    /// </summary>
    public class FirebaseItemRepository : IItemRepository
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IAppCoreDataStore _googleDataStore; // Adapter for Google's interface
        private readonly IFirebaseIndexManager _indexManager;
        private readonly ILogger<FirebaseItemRepository> _logger;
        // Path constants
        private const string ItemsPath = "items";
        private const string ItemsByUserPath = "items_by_user";
        private const string ItemsByCategoryPath = "items_by_category";
        private const string ItemsByStatePath = "items_by_state";
        private const string ItemsByStatusPath = "items_by_status";
        private const string ItemsByLocationPath = "items_by_location";
        private const string ItemsByPricePath = "items_by_price";

        // Custom DataStoreAdapter implementation
        private class DataStoreAdapter : IAppCoreDataStore
        {
            private readonly IAppCoreDataStore _dataStore;

            public DataStoreAdapter(IAppCoreDataStore dataStore)
            {
                _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            }

            // Implement Google.Apis.Util.Store.IAppCoreDataStore methods
            public Task ClearAsync()
            {
                // Implement using available methods on _dataStore
                return Task.CompletedTask;
            }

            public Task DeleteAsync<T>(string key)
            {
                return _dataStore.DeleteAsync<T>(key);
            }

            public async Task<T> GetAsync<T>(string key)
            {
                return await _dataStore.GetEntityAsync<T>(key);
            }

            public Task StoreAsync<T>(string key, T value)
            {
                return _dataStore.SetEntityAsync(key, value);
            }

            // Implement the rest of IAppCoreDataStore members
            public void Dispose()
            {
                _dataStore.Dispose();
            }

            public Task InitializeAsync()
            {
                return _dataStore.InitializeAsync();
            }

            public Task<T> GetEntityAsync<T>(string path)
            {
                return _dataStore.GetEntityAsync<T>(path);
            }

            public Task<IReadOnlyCollection<T>> GetCollectionAsync<T>(string path, IQueryParameters parameters = null)
            {
                return _dataStore.GetCollectionAsync<T>(path, parameters);
            }

            public Task<T> SetEntityAsync<T>(string path, T data)
            {
                return _dataStore.SetEntityAsync<T>(path, data);
            }

            public Task<T> GetDocumentAsync<T>(string path)
            {
                return _dataStore.GetDocumentAsync<T>(path);
            }

            public Task<(string Key, T Entity)> AddEntityAsync<T>(string path, T data)
            {
                return _dataStore.AddEntityAsync<T>(path, data);
            }

            public Task UpdateEntityFieldsAsync(string path, IDictionary<string, object> updates)
            {
                return _dataStore.UpdateEntityFieldsAsync(path, updates);
            }

            public Task DeleteEntityAsync(string path)
            {
                return _dataStore.DeleteEntityAsync(path);
            }

            public Task BatchUpdateAsync(Dictionary<string, object> updates)
            {
                return _dataStore.BatchUpdateAsync(updates);
            }

            public Task BatchDeleteAsync(IEnumerable<string> paths)
            {
                return _dataStore.BatchDeleteAsync(paths);
            }

            public Task<ITransaction> BeginTransactionAsync()
            {
                return _dataStore.BeginTransactionAsync();
            }

            public Task<List<T>> GetFilteredCollectionAsync<T>(string path, IQueryParameters parameters)
            {
                return _dataStore.GetFilteredCollectionAsync<T>(path, parameters);
            }

            public Task<int> GetCollectionSizeAsync(string path)
            {
                return _dataStore.GetCollectionSizeAsync(path);
            }

            public Task StoreAsync(string key, object value)
            {
                return _dataStore.StoreAsync(key, value);
            }

            public Task UpdateEntityAsync(string v, Conversation conversation)
            {
                throw new NotImplementedException();
            }

            public Task UpdateEntityFieldsAsync(string v, Conversation conversation)
            {
                throw new NotImplementedException();
            }
        }
        public FirebaseItemRepository(
                IAppCoreDataStore dataStore,
                IFirebaseIndexManager indexManager,
                ILogger<FirebaseItemRepository> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _googleDataStore = new DataStoreAdapter(dataStore); // Create adapter for Google's interface
            _indexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get an item by its ID (works with both numeric and Firebase IDs)
        /// </summary>
        public async Task<Item?> GetByIdAsync(string id)
        {
            try
            {
                // Input id is already a string, so we can use it directly
                string firebaseId = id;
                var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{firebaseId}");

                if (firebaseItem == null)
                    return null;

                var item = firebaseItem.ToItem();

                // Ensure PostedByUser is not null to prevent issues later
                if (item != null && item.PostedByUser == null && !string.IsNullOrEmpty(item.PostedByUserId))
                {
                    _logger.LogWarning($"PostedByUser is null for item {id}, creating placeholder");
                    item.PostedByUser = new User
                    {
                        Id = item.PostedByUserId, // Already a string
                        DisplayName = "Unknown User",
                        Email = "placeholder@example.com",
                        PasswordHash = "placeholder-hash"
                    };
                }

                return item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting item by ID {id}");
                return null;
            }
        }

        public async Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria)
        {
            // Convert criteria to parameters
            var parameters = criteria.ToFilterParameters();
            return await GetFilteredAsync(parameters);
        }

        public async Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters parameters)
        {
            // Implementation to apply all filters to Firebase query
            // Include logic for the new filter types:
            // - Tags
            // - Category-specific filters (ForSaleCategory, ForRentCategory, etc.)
            // - Date range
            // - Location with radius
        }

        /// <summary>
        /// Create a new item with all indexes
        /// </summary>
        public async Task<string> CreateAsync(Item item)
        {
            try
            {
                // Validate item data
                if (item == null) throw new ArgumentNullException(nameof(item));
                if (string.IsNullOrWhiteSpace(item.Title)) throw new ArgumentException("Item title cannot be empty");

                // Set defaults
                if (item.ListedDate == default)
                    item.ListedDate = DateTime.UtcNow;

                // Convert to Firebase model with new ID
                string firebaseId = IdConversionHelper.GeneratePushId();
                var firebaseItem = FirebaseItem.FromItem(item, firebaseId);

                // Create main item and all indexes
                string entityPath = $"{ItemsPath}/{firebaseId}";

                // Fix for CS1503: Use a lambda expression instead of a method group
                await _indexManager.UpdateEntityIndexesAsync(
                    entityPath,
                    firebaseItem,
                    fi => fi.CreateIndexEntries());

                // Return the Firebase ID directly (no conversion needed)
                return firebaseId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating item: {item?.Title ?? "Unknown"}");
                return string.Empty; // Return empty string instead of -1
            }
        }

        /// <summary>
        /// Update an existing item and maintain indexes
        /// </summary>
        /// <summary>
        /// Update an existing item and maintain indexes
        /// </summary>
        public async Task<bool> UpdateAsync(Item item)
        {
            try
            {
                if (item == null) throw new ArgumentNullException(nameof(item));
                if (!string.IsNullOrEmpty(item.Id)) // Check for non-empty string ID instead of numeric comparison
                {
                    // Use the item.Id directly as the Firebase ID or convert if needed
                    string firebaseId = item.Id;

                    // Get existing item to detect changes
                    var existingFirebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{firebaseId}");
                    if (existingFirebaseItem == null)
                    {
                        _logger.LogWarning($"Item with ID {item.Id} not found for update");
                        return false;
                    }

                    // Convert to Firebase model, preserving ID and timestamps
                    var firebaseItem = FirebaseItem.FromItem(item, firebaseId);
                    firebaseItem.CreatedTimestamp = existingFirebaseItem.CreatedTimestamp;

                    // Fix for CS0229: Use reflection to avoid ambiguity
                    // Inside the UpdateAsync method
                    var properties = typeof(FirebaseItem).GetProperties();
                    var versionProperty = properties.FirstOrDefault(p => p.Name == "Version" && p.PropertyType == typeof(string));
                    if (versionProperty != null)
                    {
                        string currentVersion = (string)(versionProperty.GetValue(existingFirebaseItem) ?? "0");
                        int versionNum = 0;
                        if (int.TryParse(currentVersion, out versionNum))
                        {
                            versionProperty.SetValue(firebaseItem, (versionNum + 1).ToString());
                        }
                        else
                        {
                            versionProperty.SetValue(firebaseItem, "1");
                        }
                    }

                    // Update indexes - the index manager will handle the diff
                    string entityPath = $"{ItemsPath}/{firebaseId}";

                    // Fix for CS1503: Use a lambda expression
                    await _indexManager.UpdateEntityIndexesAsync(
                        entityPath,
                        firebaseItem,
                        fi => fi.CreateIndexEntries());

                    return true;
                }

                throw new ArgumentException("Item ID must be valid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item {item?.Id}");
                return false;
            }
        }


        /// <summary>
        /// Delete an item and all its indexes
        /// </summary>
        public async Task<bool> DeleteAsync(string id)
        {
            try
            {
                // Convert numeric ID to Firebase ID
                string firebaseId = await IdConversionHelper.ConvertToFirebaseIdAsync(_googleDataStore, id, nameof(Item));

                // Get the existing item to get index entries
                var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{firebaseId}");
                if (firebaseItem == null)
                {
                    _logger.LogWarning($"Item with ID {id} not found for deletion");
                    return false;
                }

                // Remove all indexes and the item itself
                string entityPath = $"{ItemsPath}/{firebaseId}";

                // Fix for method group conversion
                await _indexManager.RemoveEntityIndexesAsync(
                    entityPath,
                    firebaseItem,
                    fi => fi.CreateIndexEntries());

                // Remove ID mapping
                var updates = new Dictionary<string, object>
                {
                    [$"id_mappings/Item/numeric/{id}"] = (object)null!,
                    [$"id_mappings/Item/firebase/{firebaseId}"] = (object)null!
                };
                await _dataStore.BatchUpdateAsync(updates);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item {id}");
                return false;
            }
        }

        /// <summary>
        /// Get filtered items using optimized queries
        /// </summary>
        public async Task<IEnumerable<Item>> GetFilteredAsync(FilterParameters filter)
        {
            try
            {
                // Determine the most efficient index to use
                var primaryIndexPath = DeterminePrimaryIndexPath(filter);

                if (primaryIndexPath != null)
                {
                    // Use index-based query
                    var items = await QueryUsingIndexAsync(primaryIndexPath, filter);
                    return ApplyClientSideFilters(items, filter);
                }

                // Fallback to collection scan with client-side filtering
                var allItems = await GetAllAsync();
                return ApplyClientSideFilters(allItems.ToList(), filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered items");
                return Enumerable.Empty<Item>();
            }
        }
        
        
        /// <summary>
        /// Get paginated items with Firebase cursor-based pagination
        /// </summary>
        public async Task<PaginatedResult<Item>> GetPaginatedAsync(FilterParameters filter)
        {
            try
            {
                var pageSize = filter.Take > 0 ? filter.Take : 20;
                var primaryIndexPath = DeterminePrimaryIndexPath(filter);

                // Use index-based pagination
                if (primaryIndexPath != null)
                    return await GetPaginatedUsingIndexAsync(primaryIndexPath, filter, pageSize);

                // Fallback to full collection pagination
                return await GetPaginatedFullCollectionAsync(filter, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated items");
                return new PaginatedResult<Item>
                {
                    Items = new List<Item>(),
                    TotalItems = 0,
                    Page = 1,
                    PageSize = filter?.Take ?? 20,
                    TotalPages = 0,
                    HasNextPage = false,
                    HasPreviousPage = false
                };
            }
        }


        /// <summary>
        /// Get items by user ID using optimized index
        /// </summary>
        public async Task<IEnumerable<Item>> GetByUserIdAsync(string userId)
        {
            try
            {
                // userId is already a string, use it directly 
                string firebaseUserId = userId;

                // Use denormalized index
                var itemEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>($"{ItemsByUserPath}/{firebaseUserId}");

                if (itemEntries == null || !itemEntries.Any())
                    return Enumerable.Empty<Item>();

                // Get full items in parallel
                var itemTasks = itemEntries.Select(async entry =>
                {
                    string? firebaseItemId = entry.Keys.FirstOrDefault();
                    if (string.IsNullOrEmpty(firebaseItemId)) return null;

                    // Use the firebaseItemId directly as a string
                    return await GetByIdAsync(firebaseItemId);
                });

                var items = await Task.WhenAll(itemTasks);

                // Filter out nulls and sort by date
                return items
                    .Where(item => item != null)
                    .Select(item => item!)
                    .OrderByDescending(item => item.ListedDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting items for user {userId}");
                return Enumerable.Empty<Item>();
            }
        }


        /// <summary>
        /// Get all items
        /// </summary>
        public async Task<IEnumerable<Item>> GetAllAsync()
        {
            try
            {
                var firebaseItems = await _dataStore.GetCollectionAsync<FirebaseItem>(ItemsPath);

                var items = firebaseItems
                    .Where(fi => fi != null)
                    .Select(fi => fi.ToItem())
                    .Where(item => item != null)
                    .ToList();

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all items");
                return Enumerable.Empty<Item>();
            }
        }


        /// <summary>
        /// Increment the view count for an item
        /// </summary>
        public async Task<bool> IncrementViewCountAsync(string itemId)
        {
            try
            {
                // Convert numeric ID to Firebase ID
                string firebaseId = await IdConversionHelper.ConvertToFirebaseIdAsync(_googleDataStore, itemId, nameof(Item));

                // Create an increment operation using object for ServerValue
                var increment = new { increment = 1 };
                // Use Firebase's built-in increment operation
                var updates = new Dictionary<string, object>
                {
                    [$"{ItemsPath}/{firebaseId}/viewCount"] = increment,
                    [$"{ItemsPath}/{firebaseId}/version"] = increment,
                    [$"{ItemsPath}/{firebaseId}/lastModified"] = DateTime.UtcNow
                };

                await _dataStore.BatchUpdateAsync(updates);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error incrementing view count for item {itemId}");
                return false;
            }
        }
        
        
        /// <summary>
        /// Get statistics for an item
        /// </summary>
        public async Task<ItemStatistics?> GetStatisticsAsync(string itemId)
        {
            try
            {
                string firebaseId = await IdConversionHelper.ConvertToFirebaseIdAsync(_googleDataStore, itemId, nameof(Item));
                var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{firebaseId}");
                if (firebaseItem == null)
                    return null;

                return new ItemStatistics
                {
                    ItemId = itemId,
                    ViewCount = firebaseItem.ViewCount,
                    InquiryCount = firebaseItem.InquiryCount,
                    FavoriteCount = firebaseItem.FavoriteCount,
                    RatingCount = firebaseItem.RatingCount,
                    AverageRating = firebaseItem.AverageRating ?? 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting statistics for item {itemId}");
                return null;
            }
        }

        /// <summary>
        /// Update the status of an item atomically
        /// </summary>
        public async Task<bool> UpdateStatusAsync(string itemId, ItemStatus status)
        {
            try
            {
                string firebaseId = await IdConversionHelper.ConvertToFirebaseIdAsync(_googleDataStore, itemId, nameof(Item));

                // Create an increment operation using object for ServerValue
                var increment = new { increment = 1 };
                var updates = new Dictionary<string, object>
                {
                    [$"{ItemsPath}/{firebaseId}/status"] = status.ToString(),
                    [$"{ItemsPath}/{firebaseId}/version"] = increment,
                    [$"{ItemsPath}/{firebaseId}/lastModified"] = DateTime.UtcNow
                };

                // If marking as sold or rented, update availability date
                if (status == ItemStatus.Sold || status == ItemStatus.Rented)
                    updates[$"{ItemsPath}/{firebaseId}/availableTo"] = DateTime.UtcNow.ToString("o");

                await _dataStore.BatchUpdateAsync(updates);

                // Update status index
                var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"{ItemsPath}/{firebaseId}");
                if (firebaseItem != null)
                {
                    await _indexManager.UpdateEntityIndexesAsync(
                        $"{ItemsPath}/{firebaseId}",
                        firebaseItem,
                        fi => fi.CreateIndexEntries());
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for item {itemId}");
                return false;
            }
        }

        private string? DeterminePrimaryIndexPath(FilterParameters filter)
        {
            // Choose the most selective index based on filter parameters
            if (!string.IsNullOrEmpty(filter.UserId)) // Fix: Check for null or empty string instead of HasValue
                return $"{ItemsByUserPath}/{filter.UserId}";

            if (!string.IsNullOrEmpty(filter.Category))
                return $"{ItemsByCategoryPath}/{filter.Category}";

            if (filter.State.HasValue)
                return $"{ItemsByStatePath}/{filter.State.Value}";

            if (filter.Status.HasValue)
                return $"{ItemsByStatusPath}/{filter.Status.Value}";

            // For price range queries
            if (filter.MinPrice.HasValue || filter.MaxPrice.HasValue)
            {
                var minBucket = GetPriceBucket(filter.MinPrice ?? 0);
                return $"{ItemsByPricePath}/{minBucket}";
            }

            return null; // No suitable index found
        }

        private async Task<List<Item>> QueryUsingIndexAsync(string indexPath, FilterParameters filter)
        {
            // Get item IDs from the index
            var itemEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>(indexPath);

            if (itemEntries == null || !itemEntries.Any())
                return new List<Item>();

            // Get full items
            var items = new List<Item>();
            foreach (var entry in itemEntries)
            {
                string? firebaseId = entry.Keys.FirstOrDefault();
                if (string.IsNullOrEmpty(firebaseId)) continue;

                // Use the firebaseId directly, no need to convert
                var item = await GetByIdAsync(firebaseId);
                if (item != null) items.Add(item);
            }

            return items;
        }

        private async Task<PaginatedResult<Item>> GetPaginatedUsingIndexAsync(
                        string indexPath,
                        FilterParameters filter,
                        int pageSize)
        {
            // Create a proper QueryParameters object that your IAppCoreDataStore implementation accepts
            var queryParams = new ItemQueryParameters
            {
                Skip = filter.Skip,
                Take = pageSize + 1 // Get one extra to check for next page
            };

            var itemEntries = await _dataStore.GetCollectionAsync<Dictionary<string, object>>(indexPath, queryParams);

            // Rest of method unchanged
            if (itemEntries == null || !itemEntries.Any())
                return EmptyPaginatedResult(pageSize);

            // Get item details
            var items = new List<Item>();
            foreach (var entry in itemEntries.Take(pageSize))
            {
                string? firebaseId = entry.Keys.FirstOrDefault();
                if (string.IsNullOrEmpty(firebaseId)) continue;

                // No need to convert to numeric ID since we're now using string IDs
                var item = await GetByIdAsync(firebaseId);
                if (item != null) items.Add(item);
            }

            // Apply client-side filters and sorting
            items = ApplyClientSideFilters(items, filter);

            // Calculate pagination info
            bool hasNextPage = itemEntries.Count() > pageSize;
            int currentPage = (filter.Skip / pageSize) + 1;

            return new PaginatedResult<Item>
            {
                Items = items,
                TotalItems = items.Count,
                Page = currentPage,
                PageSize = pageSize,
                TotalPages = hasNextPage ? currentPage + 1 : currentPage,
                HasNextPage = hasNextPage,
                HasPreviousPage = currentPage > 1
            };
        }
        private async Task<PaginatedResult<Item>> GetPaginatedFullCollectionAsync(
            FilterParameters filter,
            int pageSize)
        {
            // Inside GetPaginatedFullCollectionAsync or similar
            var allItems = await GetAllAsync();
            var filteredItems = ApplyClientSideFilters(allItems.ToList(), filter); // Add .ToList()
                                                                                   // ...
                                                                                   // Apply pagination
            var pagedItems = filteredItems
                .Skip(filter.Skip)
                .Take(pageSize)
                .ToList();

            int totalPages = (int)Math.Ceiling(filteredItems.Count / (double)pageSize);
            int currentPage = (filter.Skip / pageSize) + 1;

            return new PaginatedResult<Item>
            {
                Items = pagedItems,
                TotalItems = filteredItems.Count,
                Page = currentPage,
                PageSize = pageSize,
                TotalPages = totalPages,
                HasNextPage = currentPage < totalPages,
                HasPreviousPage = currentPage > 1
            };
        }

        private List<Item> ApplyClientSideFilters(List<Item> items, FilterParameters filter)
        {
            // Apply any additional filters that couldn't be handled by indexes
            var query = items.AsQueryable();

            if (filter.MinPrice.HasValue)
                query = query.Where(i => i.Price >= filter.MinPrice.Value);

            if (filter.MaxPrice.HasValue)
                query = query.Where(i => i.Price <= filter.MaxPrice.Value);

            // Fix for missing SearchTerm in FilterParameters
            var searchTermProp = filter.GetType().GetProperty("SearchTerm");
            if (searchTermProp != null)
            {
                string? searchTerm = searchTermProp.GetValue(filter) as string;

                if (!string.IsNullOrEmpty(searchTerm))
                {
                    query = query.Where(i =>
                        i.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                        i.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Fix for missing SortBy in FilterParameters
            var sortByProp = filter.GetType().GetProperty("SortBy");
            var sortDescendingProp = filter.GetType().GetProperty("SortDescending");

            if (sortByProp != null && sortDescendingProp != null)
            {
                string? sortBy = sortByProp.GetValue(filter)?.ToString();
                bool sortDescending = (bool)(sortDescendingProp.GetValue(filter) ?? false);

                if (sortBy == "price")
                    query = sortDescending ?
                        query.OrderByDescending(i => i.Price) :
                        query.OrderBy(i => i.Price);
                else
                    query = sortDescending ?
                        query.OrderByDescending(i => i.ListedDate) :
                        query.OrderBy(i => i.ListedDate);
            }
            else
            {
                // Default sorting by listed date
                query = query.OrderByDescending(i => i.ListedDate);
            }

            return query.ToList();
        }

        private string GetPriceBucket(decimal price)
        {
            // Create price buckets for efficient range queries
            if (price < 10) return "0-10";
            if (price < 50) return "10-50";
            if (price < 100) return "50-100";
            if (price < 500) return "100-500";
            if (price < 1000) return "500-1000";
            if (price < 5000) return "1000-5000";
            return "5000+";
        }

        private PaginatedResult<Item> EmptyPaginatedResult(int pageSize)
        {
            return new PaginatedResult<Item>
            {
                Items = new List<Item>(),
                TotalItems = 0,
                Page = 1,
                PageSize = pageSize,
                TotalPages = 0,
                HasNextPage = false,
                HasPreviousPage = false
            };
        }


        #region Implemented Interface Methods

        /// <summary>
        /// Increment the inquiry count for an item
        /// </summary>
        public async Task<bool> IncrementInquiryCountAsync(string itemId)
        {
            try
            {
                // Convert numeric ID to Firebase ID
                string firebaseId = await IdConversionHelper.ConvertToFirebaseIdAsync(_googleDataStore, itemId, nameof(Item));

                // Create an increment operation using object for ServerValue
                var increment = new { increment = 1 };
                // Use Firebase's built-in increment operation
                var updates = new Dictionary<string, object>
                {
                    [$"{ItemsPath}/{firebaseId}/inquiryCount"] = increment,
                    [$"{ItemsPath}/{firebaseId}/version"] = increment,
                    [$"{ItemsPath}/{firebaseId}/lastModified"] = DateTime.UtcNow
                };

                await _dataStore.BatchUpdateAsync(updates);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error incrementing inquiry count for item {itemId}");
                return false;
            }
        }
        /// <summary>
        /// Check if an item is available
        /// </summary>
        public async Task<bool> IsAvailableAsync(string itemId)
        {
            try
            {
                var item = await GetByIdAsync(itemId);
                if (item == null)
                    return false;

                // Check if item is active and available
                return item.Status == ItemStatus.Active &&
                       (item.AvailableTo == null || item.AvailableTo > DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking availability for item {itemId}");
                return false;
            }
        }


        /// <summary>
        /// Get items by state
        /// </summary>
        /// <summary>
        /// Get items by state
        /// </summary>
        public async Task<IEnumerable<Item>> GetByStateAsync(AlState state, FilterParameters? additionalFilters = null)
        {
            try
            {
                // Use state index
                var indexPath = $"{ItemsByStatePath}/{state}";
                var items = await QueryUsingIndexAsync(indexPath, additionalFilters ?? new FilterParameters());

                // Apply additional filters if needed
                if (additionalFilters != null)
                    return ApplyClientSideFilters(items, additionalFilters);

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting items by state {state}");
                return Enumerable.Empty<Item>();
            }
        }
      
        
        /// <summary>
        /// Get filtered items based on criteria
        /// </summary>
        public async Task<IEnumerable<Item>> GetFilteredByCriteriaAsync(FilterCriteria criteria)
        {
            try
            {
                // Convert criteria to filter parameters
                var filter = ConvertCriteriaToParameters(criteria);
                return await GetFilteredAsync(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered items by criteria");
                return Enumerable.Empty<Item>();
            }
        }


        /// <summary>
        /// Get items by category
        /// </summary>
        public async Task<IEnumerable<Item>> GetByCategoryAsync(string category, FilterParameters? additionalFilters = null)
        {
            try
            {
                if (string.IsNullOrEmpty(category))
                    throw new ArgumentException("Category cannot be null or empty");

                // Use category index
                var indexPath = $"{ItemsByCategoryPath}/{category}";
                var items = await QueryUsingIndexAsync(indexPath, additionalFilters ?? new FilterParameters());

                // Apply additional filters if needed
                if (additionalFilters != null)
                    return ApplyClientSideFilters(items, additionalFilters);

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting items by category {category}");
                return Enumerable.Empty<Item>();
            }
        }



        /// <summary>
        /// Get paginated items by criteria
        /// </summary>
        public async Task<PaginatedResult<Item>> GetPaginatedByCriteriaAsync(FilterCriteria criteria)
        {
            try
            {
                // Convert criteria to filter parameters
                var filter = ConvertCriteriaToParameters(criteria);
                return await GetPaginatedAsync(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated items by criteria");
                return new PaginatedResult<Item>
                {
                    Items = new List<Item>(),
                    TotalItems = 0,
                    Page = 1,
                    PageSize = criteria?.PageSize ?? 20,
                    TotalPages = 0,
                    HasNextPage = false,
                    HasPreviousPage = false
                };
            }
        }

        /// <summary>
        /// Search items by text
        /// </summary>
        public async Task<IEnumerable<Item>> SearchByTextAsync(string searchText)
        {
            try
            {
                if (string.IsNullOrEmpty(searchText))
                    return Enumerable.Empty<Item>();

                // Get all items and filter by text
                var allItems = await GetAllAsync();

                return allItems.Where(item =>
                    item.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    item.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    (item.PhotoUrls != null && item.PhotoUrls.Any(url =>
                        url.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
                ).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching items by text '{searchText}'");
                return Enumerable.Empty<Item>();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Convert filter criteria to filter parameters
        /// </summary>
        /// <summary>
        /// Convert filter criteria to filter parameters
        /// </summary>
        private FilterParameters ConvertCriteriaToParameters(FilterCriteria criteria)
        {
            if (criteria == null)
                return new FilterParameters();

            var parameters = new FilterParameters
            {
                Skip = (criteria.Page - 1) * criteria.PageSize,
                Take = criteria.PageSize
            };

            // Map criteria fields to parameters
            var userIdProp = criteria.GetType().GetProperty("UserId");
            if (userIdProp != null)
            {
                // Convert int? userId to string
                var userIdValue = userIdProp.GetValue(criteria);
                if (userIdValue != null)
                {
                    parameters.UserId = userIdValue.ToString();
                }
            }

            var categoryProp = criteria.GetType().GetProperty("Category");
            if (categoryProp != null)
            {
                parameters.Category = (string?)categoryProp.GetValue(criteria);
            }

            var stateProp = criteria.GetType().GetProperty("State");
            if (stateProp != null)
            {
                parameters.State = (AlState?)stateProp.GetValue(criteria);
            }

            var statusProp = criteria.GetType().GetProperty("Status");
            if (statusProp != null)
            {
                parameters.Status = (ItemStatus?)statusProp.GetValue(criteria);
            }

            var minPriceProp = criteria.GetType().GetProperty("MinPrice");
            if (minPriceProp != null)
            {
                parameters.MinPrice = (decimal?)minPriceProp.GetValue(criteria);
            }

            var maxPriceProp = criteria.GetType().GetProperty("MaxPrice");
            if (maxPriceProp != null)
            {
                parameters.MaxPrice = (decimal?)maxPriceProp.GetValue(criteria);
            }

            // Add dynamic properties for SearchTerm, SortBy, and SortDescending
            var searchTermProp = criteria.GetType().GetProperty("SearchTerm");
            if (searchTermProp != null)
            {
                string? searchTerm = (string?)searchTermProp.GetValue(criteria);

                // Use reflection to add property if it exists on FilterParameters
                var paramSearchTermProp = parameters.GetType().GetProperty("SearchTerm");
                if (paramSearchTermProp != null && searchTerm != null)
                {
                    paramSearchTermProp.SetValue(parameters, searchTerm);
                }
            }

            var sortByProp = criteria.GetType().GetProperty("SortBy");
            if (sortByProp != null)
            {
                string? sortBy = (string?)sortByProp.GetValue(criteria);

                // Use reflection to add property if it exists on FilterParameters
                var paramSortByProp = parameters.GetType().GetProperty("SortBy");
                if (paramSortByProp != null && sortBy != null)
                {
                    paramSortByProp.SetValue(parameters, sortBy);
                }
            }

            var sortDescendingProp = criteria.GetType().GetProperty("SortDescending");
            if (sortDescendingProp != null)
            {
                bool? sortDescending = (bool?)sortDescendingProp.GetValue(criteria);

                // Use reflection to add property if it exists on FilterParameters
                var paramSortDescendingProp = parameters.GetType().GetProperty("SortDescending");
                if (paramSortDescendingProp != null && sortDescending.HasValue)
                {
                    paramSortDescendingProp.SetValue(parameters, sortDescending.Value);
                }
            }

            // Return the parameters at the end of the method
            return parameters;
        }
    }
}
#endregion