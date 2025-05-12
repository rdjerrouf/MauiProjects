using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Services.Application.Security.Implementations.Interfaces
{
    public interface ISecurityService
    {
        // Report management
        Task<Report> ReportItemAsync(string itemId, string reportedByUserId, string reason, string? additionalComments = null);
        Task<List<Report>> GetUserReportsAsync(string userId);
        Task<bool> HasUserReportedItemAsync(string userId, string itemId);

        // User blocking
        Task<BlockedUser> BlockUserAsync(string userId, string blockedUserId, string? reason = null);
        Task<bool> UnblockUserAsync(string userId, string blockedUserId);
        Task<bool> IsUserBlockedAsync(string userId, string blockedUserId);
        Task<List<User>> GetBlockedUsersAsync(string userId);
    }

}