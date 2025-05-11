// MarketDZ.Services.UserServices/IUserAuthService.cs
using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using MarketDZ.Services.Repositories;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Users.Interfaces
{
    public interface IUserAuthService
    {
        // Core methods
        Task<User?> GetCurrentUserAsync();
        Task<AuthResult> SignInAsync(string email, string password);
        Task<AuthResult> SignOutAsync();
        Task<AuthResult> RegisterUserAsync(UserRegistrationDto registrationDto);

        // Auth-related methods
        /// <summary>
        /// Checks if an email address is already in use
        /// </summary>
        Task<AuthResult> IsEmailInUseAsync(string email);
        Task<AuthResult> ConfirmEmailAsync(string token);
        Task<AuthResult> ConfirmEmailAsync(string userId, string token);
        Task<AuthResult> SendPasswordResetAsync(string email);
        Task<AuthResult> ResetPasswordAsync(string token, string newPassword);
        Task<AuthResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword);
        Task<AuthResult> ConfirmRegistrationAsync(string token);
        Task<bool> CreateVerificationTokenAsync(string userId, string token, VerificationType verificationType);
        string GenerateVerificationToken();

        // User profile methods
        Task<User?> GetUserByIdAsync(string userId);
        /// <summary>
        /// Gets a user profile for display
        /// </summary>
        Task<UserProfileDto?> GetUserProfileAsync(string userId);

        // Email verification methods
        /// <summary>
        /// Checks if a user's email is verified
        /// </summary>
        Task<AuthResult> IsEmailVerifiedAsync(string userId);
        /// <summary>
        /// Generates an email verification token for a user
        /// </summary>
        Task<AuthResult> GenerateEmailVerificationTokenAsync(User user);
    }
}
