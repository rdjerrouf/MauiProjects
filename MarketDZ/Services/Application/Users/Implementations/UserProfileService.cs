using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Services.DbServices;
using MarketDZ.Services.Media;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


using IUserRepository = MarketDZ.Services.Repositories.IUserRepository;

namespace MarketDZ.Services.Application.Users.Implementations
{
    public class UserProfileService : IUserProfileService
    {
        private readonly IUserRepository _userRepository;
        private readonly IItemRepository _itemRepository;
        private readonly IMediaService _mediaService;
        private readonly ILogger<UserProfileService> _logger;

        public UserProfileService(
            IUserRepository userRepository,
            IItemRepository itemRepository,
            IMediaService mediaService,
            ILogger<UserProfileService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _itemRepository = itemRepository ?? throw new ArgumentNullException(nameof(itemRepository));
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Helper method to safely convert userId and get user
        private async Task<User> GetUserByStringIdAsync(string userId)
        {
            // Convert userId to string as per IUserRepository.GetByIdAsync signature
            return await _userRepository.GetByIdAsync(userId);
        }
        public async Task<User> GetUserByEmailAsync(string email)
        {
            try
            {
                return await _userRepository.GetByEmailAsync(email) ?? new User
                {
                    Email = email,
                    PasswordHash = ""
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by email {Email}", email);
                return new User
                {
                    Email = email,
                    PasswordHash = ""
                };
            }
        }
        // Add to UserProfileService.cs

        // 1. GetCurrentUserAsync
        public async Task<User> GetCurrentUserAsync()
        {
            try
            {
                // Simulate an asynchronous operation to avoid CS1998 warning
                return await Task.FromResult<User>(null!); // Replace null! with actual logic when implemented
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                throw;
            }
        }

        //  GetUserProfileAsync
        public async Task<UserProfileDto> GetUserProfileAsync(string userId)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot get profile: User {UserId} not found", userId);
                    return null!;
                }

                // Get user's items for counts
                var userItems = await _itemRepository.GetByUserIdAsync(userId);

                // Create and return the profile DTO
                return new UserProfileDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    DisplayName = user.DisplayName,
                    ProfilePicture = user.ProfilePicture,
                    Bio = user.Bio,
                    CreatedAt = user.CreatedAt,
                    PhoneNumber = user.PhoneNumber,
                    City = user.City,
                    Province = user.Province,
                    PostedItemsCount = userItems.Count(),
                    FavoriteItemsCount = 0, // You may need to implement this
                    Photos = new List<FileResult>() // Initialize empty photo collection
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting profile for user {UserId}", userId);
                return null!;
            }
        }

        //  UpdateUserProfileAsync 
        public async Task<bool> UpdateUserProfileAsync(
            string userId,
            string displayName,
            string profilePicture,
            string bio)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);   
                if (user == null)
                {
                    _logger.LogWarning("Cannot update profile: User {UserId} not found", userId);
                    return false;
                }

                // Update profile-specific fields
                if (!string.IsNullOrEmpty(displayName))
                    user.DisplayName = displayName;

                if (!string.IsNullOrEmpty(bio))
                    user.Bio = bio;

                if (!string.IsNullOrEmpty(profilePicture))
                    user.ProfilePicture = profilePicture;

                // Save the updated user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                return false;
            }
        }


        //  UpdateUserContactInfoAsync
        public async Task<bool> UpdateUserContactInfoAsync(
            string userId,
            string phoneNumber,
            string city,
            string province)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update contact info: User {UserId} not found", userId);
                    return false;
                }

                // Update contact-related fields
                if (!string.IsNullOrEmpty(phoneNumber))
                    user.PhoneNumber = phoneNumber;

                if (!string.IsNullOrEmpty(city))
                    user.City = city;

                if (!string.IsNullOrEmpty(province))
                    user.Province = province;

                // Save the updated user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating contact info for user {UserId}", userId);
                return false;
            }
        }
        //  UpdateUserPrivacyAsync
        public async Task<bool> UpdateUserPrivacyAsync(string userId, bool showEmail, bool showPhone)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update privacy settings: User {UserId} not found", userId);
                    return false;
                }

                // Update privacy settings
                // Note: You'll need to ensure these properties exist in your User model
                user.ShowEmail = showEmail;
                user.ShowPhone = showPhone;

                // Save the updated user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating privacy settings for user {UserId}", userId);
                return false;
            }
        }

        //  SendEmailVerificationTokenAsync
        public async Task<bool> SendEmailVerificationTokenAsync(string userId)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot send verification token: User {UserId} not found", userId);
                    return false;
                }

                // This would typically involve:
                // 1. Generating a verification token
                // 2. Storing it in the database
                // 3. Sending an email with a verification link

                // As email sending is not implemented, log a warning
                _logger.LogWarning("Email verification token requested but email sending not implemented");

                // Set a placeholder token
                user.EmailVerificationToken = Guid.NewGuid().ToString();

                // Update the user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending verification token for user {UserId}", userId);
                return false;
            }
        }
        public async Task<bool> UpdateProfileAsync(string userId, UserProfileUpdateDto profileDto)
        {
            try
            {
                // Get the existing user
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot update profile: User {UserId} not found", userId);
                    return false;
                }

                // Update user properties
                if (!string.IsNullOrEmpty(profileDto.DisplayName))
                    user.DisplayName = profileDto.DisplayName;

                if (!string.IsNullOrEmpty(profileDto.Bio))
                    user.Bio = profileDto.Bio;

                if (!string.IsNullOrEmpty(profileDto.PhoneNumber))
                    user.PhoneNumber = profileDto.PhoneNumber;

                if (!string.IsNullOrEmpty(profileDto.City))
                    user.City = profileDto.City;

                if (!string.IsNullOrEmpty(profileDto.Province))
                    user.Province = profileDto.Province;

                if (profileDto.State.HasValue)
                    user.State = profileDto.State.Value;

                // Update profile picture if provided
                if (profileDto.ProfilePicture != null)
                {
                    await UploadProfileImageAsync(userId, profileDto.ProfilePicture);
                }
                else if (!string.IsNullOrEmpty(profileDto.ProfilePictureUrl))
                {
                    user.ProfilePicture = profileDto.ProfilePictureUrl;
                }

                // Save the updated user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UploadProfileImageAsync(string userId, FileResult imageFile)
        {
            try
            {
                // Get the existing user
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot upload profile image: User {UserId} not found", userId);
                    return false;
                }

                // Open the file stream and pass the file name to the UploadImageAsync method
                using var fileStream = await imageFile.OpenReadAsync();
                var result = await _mediaService.UploadImageAsync(fileStream, imageFile.FileName);
                if (string.IsNullOrEmpty(result.downloadUrl))
                {
                    _logger.LogWarning("Image upload failed for user {UserId}", userId);
                    return false;
                }

                // Update the user's profile picture URL
                user.ProfilePicture = result.downloadUrl;

                // Save the updated user
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading profile image for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> DeleteProfileImageAsync(string userId)
        {
            try
            {
                // Get the existing user
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot delete profile image: User {UserId} not found", userId);
                    return false;
                }

                // If there's a profile picture URL, try to delete it from storage
                if (!string.IsNullOrEmpty(user.ProfilePicture))
                {
                    // Try to delete the image from storage if needed
                    // await _mediaService.DeleteImageAsync(user.ProfilePicture);

                    // Clear the profile picture URL
                    user.ProfilePicture = null;

                    // Save the updated user
                    return await _userRepository.UpdateAsync(user);
                }

                return true; // No image to delete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting profile image for user {UserId}", userId);
                return false;
            }
        }

        public async Task<IEnumerable<Item>> GetUserRecentItemsAsync(string userId, int count = 5)
        {
            try
            {
                // Get all items by this user
                var userItems = await _itemRepository.GetByUserIdAsync(userId);

                // Return the most recent ones, limited by count
                return userItems
                    .OrderByDescending(i => i.ListedDate)
                    .Take(count)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent items for user {UserId}", userId);
                return new List<Item>();
            }
        }

        public async Task<UserProfileStatistics> GetUserStatisticsAsync(string userId)
        {
            try
            {
                // Get the user
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("Cannot get statistics: User {UserId} not found", userId);
                    return new UserProfileStatistics { UserId = userId };
                }

                // Get user's items
                var userItems = await _itemRepository.GetByUserIdAsync(userId);

                // Calculate statistics
                return new UserProfileStatistics
                {
                    UserId = userId,
                    TotalListings = userItems.Count(), // Fixed: Add parentheses to call the method
                    ActiveListings = userItems.Count(i => i.Status == ItemStatus.Active),
                    TotalViews = userItems.Sum(i => i.ViewCount),
                    AverageRating = userItems.Any(i => i.RatingCount > 0)
                        ? userItems.Where(i => i.RatingCount > 0).Average(i => i.AverageRating ?? 0)
                        : 0,
                    JoinedDate = user.CreatedAt
                    // Add other statistics as needed
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating statistics for user {UserId}", userId);
                return new UserProfileStatistics { UserId = userId };
            }
        }

        //  IsUserVerifiedAsync to use IsEmailVerified instead of IsVerified
        public async Task<bool> IsUserVerifiedAsync(string userId)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                return user?.IsEmailVerified ?? false; // Use IsEmailVerified instead of IsVerified
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking verification status for user {UserId}", userId);
                return false;
            }
        }


        public async Task<User> GetUserByIdAsync(string userId)
        {
            try
            {
                var user = await GetUserByStringIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    return new User
                    {
                        Id = userId,
                        Email = "unknown@example.com",
                        PasswordHash = ""
                    };
                }
                return user;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user {UserId}", userId);
                return new User
                {
                    Id = userId,
                    Email = "unknown@example.com",
                    PasswordHash = ""
                };
            }
        }

    }
}