using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Models.Filters;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Items.Implementations
{
    /// <summary>
    /// Handles core CRUD operations and status management for Items.
    /// </summary>
    public class ItemCoreService : IItemCoreService
    {
        private readonly IItemRepository _itemRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ItemCoreService> _logger;

        public ItemCoreService(
            IItemRepository itemRepository,
            IUserRepository userRepository,
            ILogger<ItemCoreService> logger)
        {
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Item CRUD Operations

        /// <summary>
        /// Add a basic item
        /// </summary>
        public async Task<bool> AddItemAsync(Item item)
        {
            try
            {
                _logger.LogInformation("Adding item: {Title}", item.Title);

                // Validate item
                if (!item.Validate(out string error))
                {
                    _logger.LogWarning("Item validation failed: {Error}", error);
                    return false;
                }

                // Set defaults
                if (item.ListedDate == default)
                    item.ListedDate = DateTime.UtcNow;

                // Get user if needed
                if (!string.IsNullOrEmpty(item.PostedByUserId) && item.PostedByUser == null)
                {
                    _logger.LogInformation("Fetching user with ID: {UserId}", item.PostedByUserId);
                    var user = await _userRepository.GetByIdAsync(item.PostedByUserId);

                    if (user == null)
                    {
                        _logger.LogWarning("User with ID {UserId} not found", item.PostedByUserId);
                        return false;
                    }

                    item.PostedByUser = user;
                }

                // Create the item
                var itemId = await _itemRepository.CreateAsync(item);
                _logger.LogInformation("Item created with ID: {ItemId}", itemId);

                return !string.IsNullOrEmpty(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item: {Title}", item?.Title ?? "Unknown");
                return false;
            }
        }

        /// <summary>
        /// Add a for-sale item - Note that photo handling is done by caller after item is created
        /// </summary>
        public async Task<string?> AddForSaleItemAsync(string userId, CreateForSaleItemDto itemDto)
        {
            try
            {
                // Validate user
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", userId);
                    return null;
                }

                // Create the item with proper enum category
                var item = new Item
                {
                    Title = itemDto.Title,
                    Description = itemDto.Description,
                    Price = itemDto.Price,
                    Category = ItemCategory.ForSale,
                    PostedByUserId = userId,
                    PostedByUser = user,
                    ForSaleCategory = itemDto.ForSaleCategory,
                    State = itemDto.State,
                    ListedDate = DateTime.UtcNow,
                    Status = ItemStatus.Active
                };

                // Create the item to get an ID
                var itemId = await _itemRepository.CreateAsync(item);
                if (string.IsNullOrEmpty(itemId))
                {
                    _logger.LogWarning("Failed to create item in repository");
                    return null;
                }

                _logger.LogInformation("For sale item successfully created with ID: {ItemId}", itemId);
                return itemId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating for sale item");
                return null;
            }
        }

        /// <summary>
        /// Add a rental item - Note that photo handling is done by caller after item is created
        /// </summary>
        public async Task<string?> AddRentalItemAsync(string userId, CreateRentalItemDto itemDto)
        {
            try
            {
                // Validate user
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", userId);
                    return null;
                }

                // Create the item with proper enum category
                var item = new Item
                {
                    Title = itemDto.Title,
                    Description = itemDto.Description,
                    Price = itemDto.Price,
                    Category = ItemCategory.ForRent,
                    PostedByUserId = userId,
                    PostedByUser = user,
                    RentalPeriod = itemDto.RentalPeriod,
                    AvailableFrom = itemDto.AvailableFrom,
                    AvailableTo = itemDto.AvailableTo,
                    ForRentCategory = itemDto.ForRentCategory,
                    State = itemDto.State,
                    ListedDate = DateTime.UtcNow,
                    Status = ItemStatus.Active
                };

                // Create the item to get an ID
                var itemId = await _itemRepository.CreateAsync(item);
                if (string.IsNullOrEmpty(itemId))
                {
                    _logger.LogWarning("Failed to create rental item in repository");
                    return null;
                }

                _logger.LogInformation("Rental item successfully created with ID: {ItemId}", itemId);
                return itemId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating rental item");
                return null;
            }
        }

        /// <summary>
        /// Add a job item
        /// </summary>
        public async Task<string?> AddJobItemAsync(string userId, CreateJobItemDto itemDto)
        {
            try
            {
                // Validate user
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", userId);
                    return null;
                }

                var item = new Item
                {
                    Title = itemDto.Title,
                    Description = itemDto.Description,
                    Price = itemDto.Price,
                    Category = ItemCategory.Job,
                    PostedByUserId = userId,
                    PostedByUser = user,
                    JobType = itemDto.JobType,
                    JobCategory = itemDto.JobCategory,
                    CompanyName = itemDto.CompanyName,
                    JobLocation = itemDto.JobLocation,
                    ApplyMethod = itemDto.ApplyMethod,
                    ApplyContact = itemDto.ApplyContact,
                    AvailableFrom = itemDto.AvailableFrom,
                    RentalPeriod = itemDto.SalaryPeriod,
                    IsSalaryDisclosed = itemDto.IsSalaryDisclosed,
                    State = itemDto.State,
                    ListedDate = DateTime.UtcNow,
                    Status = ItemStatus.Active
                };

                // Create the item
                var itemId = await _itemRepository.CreateAsync(item);
                _logger.LogInformation("Job item successfully created with ID: {ItemId}", itemId);

                return !string.IsNullOrEmpty(itemId) ? itemId : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating job item for user {UserId}: {Title}", userId, itemDto?.Title ?? "Unknown");
                return null;
            }
        }

        /// <summary>
        /// Add a service item
        /// </summary>
        public async Task<string?> AddServiceItemAsync(string userId, CreateServiceItemDto itemDto)
        {
            try
            {
                // Validate user
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    _logger.LogWarning("User with ID {UserId} not found", userId);
                    return null;
                }

                var item = new Item
                {
                    Title = itemDto.Title,
                    Description = itemDto.Description,
                    Price = itemDto.Price,
                    Category = ItemCategory.Service,
                    PostedByUserId = userId,
                    PostedByUser = user,
                    ServiceType = itemDto.ServiceType,
                    ServiceCategory = itemDto.ServiceCategory,
                    ServiceAvailability = itemDto.ServiceAvailability,
                    YearsOfExperience = itemDto.YearsOfExperience,
                    NumberOfEmployees = itemDto.NumberOfEmployees,
                    ServiceLocation = itemDto.ServiceLocation,
                    State = itemDto.State,
                    ListedDate = DateTime.UtcNow,
                    Status = ItemStatus.Active
                };

                // Create the item
                var itemId = await _itemRepository.CreateAsync(item);
                _logger.LogInformation("Service item successfully created with ID: {ItemId}", itemId);

                return !string.IsNullOrEmpty(itemId) ? itemId : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating service item for user {UserId}: {Title}", userId, itemDto?.Title ?? "Unknown");
                return null;
            }
        }

        /// <summary>
        /// Update an item
        /// </summary>
        public async Task<bool> UpdateItemAsync(string userId, string itemId, ItemUpdateDto updateDto)
        {
            try
            {
                // Get the item
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null || item.PostedByUserId != userId)
                {
                    _logger.LogWarning("Item {ItemId} not found or user {UserId} is not the owner", itemId, userId);
                    return false;
                }

                // Update common properties
                item.Title = updateDto.Title ?? item.Title;
                item.Description = updateDto.Description ?? item.Description;
                item.Price = updateDto.Price;
                item.State = updateDto.State;
                item.Latitude = updateDto.Latitude;
                item.Longitude = updateDto.Longitude;

                // Based on category enum, update specific properties
                switch (item.Category)
                {
                    case ItemCategory.ForSale:
                        item.ForSaleCategory = updateDto.ForSaleCategory;
                        break;

                    case ItemCategory.ForRent:
                        item.RentalPeriod = updateDto.RentalPeriod;
                        item.AvailableFrom = updateDto.AvailableFrom;
                        item.AvailableTo = updateDto.AvailableTo;
                        item.ForRentCategory = updateDto.ForRentCategory;
                        break;

                    case ItemCategory.Job:
                        item.JobType = updateDto.JobType;
                        item.JobCategory = updateDto.JobCategory;
                        item.CompanyName = updateDto.CompanyName;
                        item.JobLocation = updateDto.JobLocation;
                        item.ApplyMethod = updateDto.ApplyMethod;
                        item.ApplyContact = updateDto.ApplyContact;
                        break;

                    case ItemCategory.Service:
                        item.ServiceType = updateDto.ServiceType;
                        item.ServiceCategory = updateDto.ServiceCategory;
                        item.ServiceAvailability = updateDto.ServiceAvailability;
                        item.YearsOfExperience = updateDto.YearsOfExperience;
                        item.ServiceLocation = updateDto.ServiceLocation;
                        break;
                }

                // Update the item
                return await _itemRepository.UpdateAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating item {ItemId}", itemId);
                return false;
            }
        }

        /// <summary>
        /// Delete an item
        /// </summary>
        public async Task<bool> DeleteItemAsync(string id)
        {
            try
            {
                // Get the item to check ownership
                var item = await _itemRepository.GetByIdAsync(id);
                if (item == null)
                {
                    _logger.LogWarning("Item {ItemId} not found for deletion", id);
                    return false;
                }

                // Delete the item
                return await _itemRepository.DeleteAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item {ItemId}", id);
                return false;
            }
        }

        /// <summary>
        /// Get an item by ID
        /// </summary>
        public async Task<Item?> GetItemAsync(string id)
        {
            try
            {
                return await _itemRepository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving item {ItemId}", id);
                return null;
            }
        }

        /// <summary>
        /// Get all items
        /// </summary>
        public async Task<ObservableCollection<Item>> GetItemsAsync()
        {
            try
            {
                var items = await _itemRepository.GetAllAsync();
                return new ObservableCollection<Item>(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items");
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Get items by user
        /// </summary>
        public async Task<ObservableCollection<Item>> GetItemsByUserAsync(string userId)
        {
            try
            {
                var items = await _itemRepository.GetByUserIdAsync(userId);
                return new ObservableCollection<Item>(items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving items for user {UserId}", userId);
                return new ObservableCollection<Item>();
            }
        }

        /// <summary>
        /// Implementation for GetUserItemsAsync - same as GetItemsByUserAsync
        /// </summary>
        public async Task<IEnumerable<object>> GetUserItemsAsync(string userId)
        {
            try
            {
                var items = await _itemRepository.GetByUserIdAsync(userId);

                // Convert to DTOs
                return items.Select(item => new ItemListDto
                {
                    Id = item.Id,
                    Title = item.Title,
                    Price = item.Price,
                    Category = item.Category.ToString(),
                    PhotoUrl = item.PhotoUrl,
                    Status = item.Status,
                    ListedDate = item.ListedDate,
                    State = item.State
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user items as DTOs for user {UserId}", userId);
                return Enumerable.Empty<object>();
            }
        }

        #endregion

        #region Status Management

        /// <summary>
        /// Update item status
        /// </summary>
        public async Task<bool> UpdateItemStatusAsync(string userId, string itemId, ItemStatus status)
        {
            try
            {
                // Get the item to check ownership
                var item = await _itemRepository.GetByIdAsync(itemId);
                if (item == null || item.PostedByUserId != userId)
                {
                    _logger.LogWarning("Item {ItemId} not found or user {UserId} is not the owner", itemId, userId);
                    return false;
                }

                return await _itemRepository.UpdateStatusAsync(itemId, status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating status for item {ItemId}", itemId);
                return false;
            }
        }

        /// <summary>
        /// Check if an item is available
        /// </summary>
        public async Task<bool> IsItemAvailableAsync(string itemId)
        {
            try
            {
                return await _itemRepository.IsAvailableAsync(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking availability for item {ItemId}", itemId);
                return false;
            }
        }

        public Task<IEnumerable<object>> GetAllItemsAsync()
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}