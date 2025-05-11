using MarketDZ.Models;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Firebase.Implementations
{
    public class FirebaseItemStatusService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly FirebaseService _firebaseService;
        private readonly ILogger<FirebaseItemStatusService> _logger;

        public FirebaseItemStatusService(
            IAppCoreDataStore dataStore,
            FirebaseService firebaseService,
            ILogger<FirebaseItemStatusService> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _firebaseService = firebaseService ?? throw new ArgumentNullException(nameof(firebaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> UpdateItemStatusAsync(string userId, string itemId, ItemStatus status)
        {
            try
            {
                // Get the item
                var item = await _firebaseService.GetItemByIdAsync(itemId);
                if (item == null)
                {
                    _logger.LogWarning($"Item {itemId} not found");
                    return false;
                }

                // Verify the user is the owner
                if (item.PostedByUserId != userId)
                {
                    _logger.LogWarning($"User {userId} is not the owner of item {itemId}");
                    return false;
                }

                // Update the status
                item.Status = status;

                // If marking as sold or rented, update availability date
                if (status == ItemStatus.Sold || status == ItemStatus.Rented)
                {
                    item.AvailableTo = DateTime.UtcNow;
                }

                return await _firebaseService.UpdateItemAsync(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating status for item {itemId}");
                return false;
            }
        }

        public async Task<bool> IsItemAvailableAsync(string itemId)
        {
            try
            {
                var item = await _firebaseService.GetItemByIdAsync(itemId);
                if (item == null)
                    return false;

                return item.Status == ItemStatus.Active &&
                       (item.AvailableTo == null || item.AvailableTo > DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking availability for item {itemId}");
                return false;
            }
        }
    }
}