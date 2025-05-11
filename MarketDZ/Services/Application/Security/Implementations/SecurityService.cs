using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using MarketDZ.Models;
using MarketDZ.Services.UserServices;

namespace MarketDZ.Services.Application.Security.Implementations.Interfaces
{
    public class SecurityService : ISecurityService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IItemCoreService _itemCoreService;
        private readonly IUserProfileService _userProfileService;
        private readonly ILogger<SecurityService> _logger;

        public SecurityService(
            IAppCoreDataStore dataStore,
            IItemCoreService itemCoreService,
            IUserProfileService userProfileService,
            ILogger<SecurityService> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _itemCoreService = itemCoreService ?? throw new ArgumentNullException(nameof(itemCoreService));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Implement all methods from interface using _dataStore, _itemCoreService, and _userProfileService

        // For example:
        public async Task<Report> ReportItemAsync(string itemId, string reportedByUserId, string reason, string? additionalComments = null)
        {
            try
            {
                // Check if the item exists
                var item = await _itemCoreService.GetItemAsync(itemId);
                if (item == null)
                    throw new Exception("Item not found");

                // Check if the user exists
                var user = await _userProfileService.GetUserByIdAsync(reportedByUserId);
                if (user == null)
                    throw new Exception("User not found");

                // Create the report
                var report = new Report
                {
                    Id = await GetNextReportIdAsync(),
                    ReportedItemId = itemId,
                    ReportedByUserId = reportedByUserId,
                    Reason = reason,
                    AdditionalComments = additionalComments,
                    ReportedAt = DateTime.UtcNow,
                    Status = ReportStatus.Pending
                };

                // Save to data store
                await _dataStore.SetEntityAsync($"reports/{report.Id}", report);
                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reporting item {itemId}");
                throw;
            }
        }

        // Implement remaining methods similarly...

        #region Helper Methods
        private async Task<List<Report>> GetAllReportsAsync()
        {
            var reports = await _dataStore.GetCollectionAsync<Report>("reports");
            return reports.ToList();
        }

        private async Task<List<BlockedUser>> GetAllBlockedUsersAsync()
        {
            var blockedUsers = await _dataStore.GetCollectionAsync<BlockedUser>("blockedUsers");
            return blockedUsers.ToList();
        }

        private async Task<string> GetNextReportIdAsync()
        {
            var reports = await GetAllReportsAsync();
            return reports.Any() ? (int.Parse(reports.Max(static r => r.Id)) + 1).ToString() : "1";
        }

        private async Task<string> GetNextBlockedUserIdAsync()
        {
            var blockedUsers = await GetAllBlockedUsersAsync();
            return blockedUsers.Any() ? (int.Parse(blockedUsers.Max(b => b.Id)) + 1).ToString() : "1";
        }

        public Task<List<Report>> GetUserReportsAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> HasUserReportedItemAsync(string userId, string itemId)
        {
            throw new NotImplementedException();
        }

        public Task<BlockedUser> BlockUserAsync(string userId, string blockedUserId, string? reason = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UnblockUserAsync(string userId, string blockedUserId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> IsUserBlockedAsync(string userId, string blockedUserId)
        {
            throw new NotImplementedException();
        }

        public Task<List<User>> GetBlockedUsersAsync(string userId)
        {
            throw new NotImplementedException();
        }
        #endregion
    }

}
