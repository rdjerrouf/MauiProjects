using MarketDZ.Models;
using MarketDZ.Models.Firebase.Base.Adapters;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Firebase.Implementations
{
    /// <summary>
    /// Firebase-specific implementation of data access
    /// </summary>
    public class FirebaseService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseService> _logger;

        public FirebaseService(IAppCoreDataStore dataStore, ILogger<FirebaseService> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Item-related methods
        public async Task<Item?> GetItemByIdAsync(string itemId)
        {
            try
            {
                var firebaseItem = await _dataStore.GetEntityAsync<FirebaseItem>($"items/{itemId}");
                return firebaseItem?.ToItem();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting item with ID {itemId}");
                return null;
            }
        }

        public async Task<ObservableCollection<Item>> GetItemsAsync()
        {
            try
            {
                var collection = await _dataStore.GetCollectionAsync<FirebaseItem>("items");
                var items = new ObservableCollection<Item>();

                foreach (var firebaseItem in collection)
                {
                    if (firebaseItem != null)
                    {
                        items.Add(firebaseItem.ToItem());
                    }
                }

                return items;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items");
                return new ObservableCollection<Item>();
            }
        }

        public async Task<int> CreateItemAsync(Item item)
        {
            try
            {
                var firebaseItem = FirebaseItem.FromItem(item);
                var result = await _dataStore.AddEntityAsync("items", firebaseItem);
                return int.TryParse(result.Key, out int id) ? id : -1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating item");
                return -1;
            }
        }

        public async Task<bool> UpdateItemAsync(Item item)
        {
            try
            {
                var firebaseItem = FirebaseItem.FromItem(item);
                await _dataStore.SetEntityAsync($"items/{item.Id}", firebaseItem);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating item {item.Id}");
                return false;
            }
        }

        public async Task<bool> DeleteItemAsync(string itemId)
        {
            try
            {
                await _dataStore.DeleteEntityAsync($"items/{itemId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting item {itemId}");
                return false;
            }
        }

        // User-related methods
        public async Task<User?> GetUserByIdAsync(string userId)
        {
            try
            {
                var firebaseUser = await _dataStore.GetEntityAsync<FirebaseUser>($"users/{userId}");
                return firebaseUser?.ToUser();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user with ID {userId}");
                return null;
            }
        }
    }
}