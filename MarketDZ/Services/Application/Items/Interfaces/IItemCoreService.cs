using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Dtos.Item;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace MarketDZ.Services.Application.Items.Iterfaces
{
    public interface IItemCoreService
    {
        /// <summary> Adds a basic item. </summary> //
        Task<bool> AddItemAsync(Item item);

        /// <summary> Adds a specific 'For Sale' item. </summary> //
        Task<string?> AddForSaleItemAsync(string userId, CreateForSaleItemDto itemDto);

        /// <summary> Adds a specific 'Rental' item. </summary> //
        Task<string?> AddRentalItemAsync(string userId, CreateRentalItemDto itemDto);

        /// <summary> Adds a specific 'Job' item. </summary> //
        Task<string?> AddJobItemAsync(string userId, CreateJobItemDto itemDto);

        /// <summary> Adds a specific 'Service' item. </summary> //
        Task<string?> AddServiceItemAsync(string userId, CreateServiceItemDto itemDto);


        /// <summary> Updates an existing item. </summary> //
        Task<bool> UpdateItemAsync(string userId, string itemId, ItemUpdateDto updateDto);

        /// <summary> Deletes an item by ID. </summary> //
        Task<bool> DeleteItemAsync(string id);

        /// <summary> Retrieves a single item by ID. </summary> //
        Task<Item?> GetItemAsync(string id); // Consider if view increment logic belongs here or in IItemStatisticsService

        /// <summary> Retrieves all items. </summary> //
        Task<ObservableCollection<Item>> GetItemsAsync();


        /// <summary> 
        /// Retrieves items by user 
        /// </summary> 
        Task<ObservableCollection<Item>> GetItemsByUserAsync(string userId);

        /// <summary> Updates the status of an item (e.g., Active, Sold, Expired). </summary> //
        Task<bool> UpdateItemStatusAsync(string userId, string itemId, ItemStatus status);

        /// <summary> Checks if an item is currently available (based on status). </summary> //
        Task<bool> IsItemAvailableAsync(string itemId);
        Task<IEnumerable<object>> GetUserItemsAsync(string userId);
        Task<IEnumerable<object>> GetAllItemsAsync();
        Task<bool> UpdateItemAsync(string userId, string itemId, ItemUpdateDto updateDto);
    }
}