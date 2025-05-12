using MarketDZ.Models.Dtos;
using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Dtos.User;

namespace MarketDZ.Services.Application.Users.Interfaces
{
    public interface IUserProfileService
    {
        // Current user methods
        Task<User> GetCurrentUserAsync();

        // Profile retrieval
        Task<User> GetUserByIdAsync(string userId);
        Task<User> GetUserByEmailAsync(string email);
        Task<UserProfileDto> GetUserProfileAsync(string userId);

        // Profile management
        Task<bool> UpdateProfileAsync(string userId, UserProfileUpdateDto profileDto);
        Task<bool> UpdateUserProfileAsync(string userId, string displayName, string profilePicture, string bio);
        Task<bool> UpdateUserContactInfoAsync(string userId, string phoneNumber, string city, string province);
        Task<bool> UpdateUserPrivacyAsync(string userId, bool showEmail, bool showPhone);
        Task<bool> UploadProfileImageAsync(string userId, FileResult imageFile);
        Task<bool> DeleteProfileImageAsync(string userId);

        // Email verification methods
        Task<bool> SendEmailVerificationTokenAsync(string userId);

        // Profile statistics
        Task<UserProfileStatistics> GetUserStatisticsAsync(string userId);
        Task<IEnumerable<Item>> GetUserRecentItemsAsync(string userId, int count = 5);
        Task<bool> IsUserVerifiedAsync(string userId);
    }
}