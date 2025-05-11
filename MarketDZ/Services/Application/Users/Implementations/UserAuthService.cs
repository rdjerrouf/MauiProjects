using MarketDZ.Models.Dtos.User;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using Firebase.Auth;
using MarketDZ.Services.Application.Users.Interfaces;
using MarketDZ.Services.Core.Interfaces.Repositories;
using MarketDZ.Services.Application.Email.Interfaces;
using MarketDZ.Models.Core.Dtos.Auth;
using MarketDZ.Models.Core;

namespace MarketDZ.Services.Application.Users.Implementations
{
    public class UserAuthService : IUserAuthService
    {
        protected readonly IDbUserRepository _userRepository;
        protected readonly ISecureStorage _secureStorage;
        protected readonly IEmailService _emailService;
        protected readonly ITemporaryRegistrationStore _temporaryRegistrationStore;
        protected readonly ILogger<UserAuthService> _logger;

        protected const string CURRENT_USER_ID_KEY = "current_user_id";
        protected const int PASSWORD_MIN_LENGTH = 6;
        protected const int VERIFICATION_TOKEN_EXPIRY_HOURS = 24;

        public UserAuthService(
            IDbUserRepository userRepository,
            ISecureStorage secureStorage,
            IEmailService emailService,
            ITemporaryRegistrationStore temporaryRegistrationStore,
            ILogger<UserAuthService> logger)
        {
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _secureStorage = secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _temporaryRegistrationStore = temporaryRegistrationStore ?? throw new ArgumentNullException(nameof(temporaryRegistrationStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<User?> GetCurrentUserAsync()
        {
            try
            {
                string? userId = await _secureStorage.GetAsync(CURRENT_USER_ID_KEY);
                if (string.IsNullOrEmpty(userId))
                {
                    return null;
                }
                return await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current user");
                return null;
            }
        }

        public virtual async Task<bool> UpdateProfileAsync(string userId, UserProfileUpdateDto profileDto)
        {
            try
            {
                // Get the current user
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning($"User not found for profile update: {userId}");
                    return false;
                }

                // Update user properties
                if (profileDto.Bio != null) user.Bio = profileDto.Bio;
                if (profileDto.City != null) user.City = profileDto.City;
                if (profileDto.Province != null) user.Province = profileDto.Province;
                if (profileDto.State.HasValue) user.State = profileDto.State.Value;

                // Save the changes
                return await _userRepository.UpdateAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating profile for user {userId}");
                return false;
            }
        }

        public virtual async Task<AuthResult> SignInAsync(string email, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                {
                    return new AuthResult { Success = false, ErrorMessage = "Email and password are required" };
                }

                var user = await _userRepository.GetByEmailAsync(email);

                if (user == null)
                {
                    return AuthResult.FailureResult("Invalid email or password");
                }

                // Check password
                bool validPassword = VerifyPasswordHash(password, user.PasswordHash);

                if (!validPassword)
                {
                    return AuthResult.FailureResult("Invalid email or password");
                }

                // Check if email is verified
                if (!user.IsEmailVerified)
                {
                    return AuthResult.FailureResult("Please verify your email before signing in");
                }

                // Save current user ID
                await _secureStorage.SetAsync(CURRENT_USER_ID_KEY, user.Id.ToString());

                return AuthResult.SuccessResult(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during sign in for email {email}");
                return AuthResult.FailureResult("An error occurred during sign-in");
            }
        }

        public virtual async Task<AuthResult> SignOutAsync()
        {
            try
            {
                _secureStorage.Remove(CURRENT_USER_ID_KEY);
                return AuthResult.SuccessResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign out");
                return AuthResult.FailureResult("An error occurred while signing out");
            }
        }

        public virtual async Task<AuthResult> RegisterUserAsync(UserRegistrationDto registrationDto)
        {
            try
            {
                if (registrationDto == null)
                {
                    return new AuthResult { Success = false, ErrorMessage = "Registration data is required" };
                }

                if (string.IsNullOrWhiteSpace(registrationDto.Email) ||
                    string.IsNullOrWhiteSpace(registrationDto.Password) ||
                    string.IsNullOrWhiteSpace(registrationDto.DisplayName))
                {
                    return new AuthResult { Success = false, ErrorMessage = "Email, password, and display name are required" };
                }

                if (registrationDto.Password.Length < PASSWORD_MIN_LENGTH)
                {
                    return new AuthResult { Success = false, ErrorMessage = $"Password must be at least {PASSWORD_MIN_LENGTH} characters" };
                }

                if (!registrationDto.AgreeToTerms)
                {
                    return new AuthResult { Success = false, ErrorMessage = "You must agree to the terms and conditions" };
                }

                // Check if email is already in use
                var emailCheckResult = await IsEmailInUseAsync(registrationDto.Email);
                if (emailCheckResult.Success)
                {
                    return AuthResult.FailureResult("Email is already registered");
                }

                // Store temporarily instead of creating user immediately
                var tempToken = await _temporaryRegistrationStore.StoreTemporaryRegistrationAsync(registrationDto);

                // Create verification link with temporary token
                string verificationLink = $"marketdz://confirm-registration?token={Uri.EscapeDataString(tempToken)}";

                // Send verification email
                var emailResult = await _emailService.SendEmailVerificationAsync(registrationDto.Email, verificationLink);

                if (!emailResult.Success)
                {
                    _logger.LogWarning("Failed to send verification email: {Error}", emailResult.ErrorMessage);
                    return AuthResult.FailureResult("Failed to send verification email");
                }

                _logger.LogInformation("Verification email sent to {Email}", registrationDto.Email);
                return AuthResult.SuccessResult(null, tempToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return AuthResult.FailureResult("An error occurred during registration");
            }
        }

        public virtual async Task<AuthResult> ConfirmRegistrationAsync(string token)
        {
            try
            {
                // Get temporary registration
                var registrationDto = await _temporaryRegistrationStore.GetTemporaryRegistrationAsync(token);
                if (registrationDto == null)
                {
                    return AuthResult.FailureResult("Invalid or expired registration token");
                }

                // Now create the actual user
                var newUser = new User
                {
                    Email = registrationDto.Email,
                    DisplayName = registrationDto.DisplayName,
                    PasswordHash = HashPassword(registrationDto.Password),
                    City = registrationDto.City,
                    Province = registrationDto.Province,
                    CreatedAt = DateTime.UtcNow,
                    IsEmailVerified = true // Email is already verified
                };

                // Save user to repository
                bool created = await _userRepository.CreateAsync(newUser);

                if (!created)
                {
                    return AuthResult.FailureResult("Failed to create user");
                }

                // Clean up temporary storage
                await _temporaryRegistrationStore.DeleteTemporaryRegistrationAsync(token);

                // Get the created user
                var createdUser = await _userRepository.GetByEmailAsync(registrationDto.Email);

                return AuthResult.SuccessResult(createdUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming registration");
                return AuthResult.FailureResult("An error occurred while confirming registration");
            }
        }

        public virtual async Task<AuthResult> IsEmailInUseAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return AuthResult.FailureResult("Email is required");
                }

                var user = await _userRepository.GetByEmailAsync(email);

                // Success means the email IS in use
                if (user != null)
                {
                    return AuthResult.SuccessResult(null);
                }

                return AuthResult.FailureResult("Email is not in use");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if email {email} is in use");
                return AuthResult.FailureResult("An error occurred while checking the email");
            }
        }

        public virtual async Task<AuthResult> ConfirmEmailAsync(string token)
        {
            try
            {
                var verificationToken = await _userRepository.GetVerificationTokenAsync(token);

                if (verificationToken == null ||
                    verificationToken.Type != Models.VerificationType.EmailVerification ||
                    verificationToken.IsUsed ||
                    verificationToken.ExpiresAt < DateTime.UtcNow)
                {
                    return AuthResult.FailureResult("Invalid or expired token");
                }

                var user = await _userRepository.GetByIdAsync(verificationToken.UserId);
                if (user == null)
                {
                    return AuthResult.FailureResult("User not found");
                }

                // Update the user
                user.IsEmailVerified = true;
                user.EmailVerifiedAt = DateTime.UtcNow;
                bool updated = await _userRepository.UpdateAsync(user);

                if (!updated)
                {
                    return AuthResult.FailureResult("Failed to update user after email verification");
                }

                // Mark token as used
                verificationToken.IsUsed = true;
                // Update the method call to use the correct type for the second argument
                await _userRepository.Update(verificationToken);

                return AuthResult.SuccessResult(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming email");
                return AuthResult.FailureResult("An error occurred while confirming the email");
            }
        }

        public virtual async Task<AuthResult> ConfirmEmailAsync(string userId, string token)
        {
            try
            {
                return await ConfirmEmailAsync(token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error confirming email for user {userId}");
                return AuthResult.FailureResult("An error occurred while confirming the email");
            }
        }

        public virtual async Task<AuthResult> SendPasswordResetAsync(string email)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(email))
                {
                    return AuthResult.FailureResult("Email is required");
                }

                var user = await _userRepository.GetByEmailAsync(email);

                if (user == null)
                {
                    // For security reasons, don't reveal that the email doesn't exist
                    return AuthResult.SuccessResult(null);
                }

                // Generate reset token
                string token = GenerateVerificationToken();
                bool tokenCreated = await CreateVerificationTokenAsync(user.Id, token, VerificationType.PasswordReset);

                if (!tokenCreated)
                {
                    return AuthResult.FailureResult("Failed to create password reset token");
                }

                // Send reset email
                await _emailService.SendPasswordResetEmailAsync(email, token);
                return AuthResult.SuccessResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending password reset for email {email}");
                return AuthResult.FailureResult("An error occurred while sending the password reset email");
            }
        }

        public virtual async Task<AuthResult> ResetPasswordAsync(string token, string newPassword)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
                {
                    return AuthResult.FailureResult("Token and new password are required");
                }

                if (newPassword.Length < PASSWORD_MIN_LENGTH)
                {
                    return AuthResult.FailureResult($"Password must be at least {PASSWORD_MIN_LENGTH} characters");
                }

                var verificationToken = await _userRepository.GetVerificationTokenAsync(token);

                if (verificationToken == null ||
                    verificationToken.Type != Models.VerificationType.PasswordReset ||
                    verificationToken.IsUsed ||
                    verificationToken.ExpiresAt < DateTime.UtcNow)
                {
                    return AuthResult.FailureResult("Invalid or expired token");
                }

                var user = await _userRepository.GetByIdAsync(verificationToken.UserId);
                if (user == null)
                {
                    return AuthResult.FailureResult("User not found");
                }

                // Update the password
                user.PasswordHash = HashPassword(newPassword);
                bool updated = await _userRepository.UpdateAsync(user);

                if (!updated)
                {
                    return AuthResult.FailureResult("Failed to update password");
                }

                // Mark token as used
                verificationToken.IsUsed = true;
                await _userRepository.UpdateVerificationTokenAsync(verificationToken);

                return AuthResult.SuccessResult(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting password");
                return AuthResult.FailureResult("An error occurred while resetting the password");
            }
        }

        public virtual async Task<AuthResult> ChangePasswordAsync(string userId, string currentPassword, string newPassword)
        {
            try
            {
                // Convert int userId to string for use with repository
                string userIdStr = userId.ToString();

                if (string.IsNullOrWhiteSpace(currentPassword) ||
                    string.IsNullOrWhiteSpace(newPassword))
                {
                    return AuthResult.FailureResult("Invalid input parameters");
                }

                if (newPassword.Length < PASSWORD_MIN_LENGTH)
                {
                    return AuthResult.FailureResult($"Password must be at least {PASSWORD_MIN_LENGTH} characters");
                }

                var user = await _userRepository.GetByIdAsync(userIdStr);
                if (user == null)
                {
                    return AuthResult.FailureResult("User not found");
                }

                // Check current password
                bool validPassword = VerifyPasswordHash(currentPassword, user.PasswordHash);
                if (!validPassword)
                {
                    return AuthResult.FailureResult("Current password is incorrect");
                }

                // Update password
                user.PasswordHash = HashPassword(newPassword);
                bool updated = await _userRepository.UpdateAsync(user);

                if (!updated)
                {
                    return AuthResult.FailureResult("Failed to update password");
                }

                return AuthResult.SuccessResult(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error changing password for user {userId}");
                return AuthResult.FailureResult("An error occurred while changing the password");
            }
        }

        public virtual async Task<User?> GetUserByIdAsync(string userId)
        {
            try
            {
                return await _userRepository.GetByIdAsync(userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting user by ID {userId}");
                return null;
            }
        }

        public virtual async Task<UserProfileDto?> GetUserProfileAsync(string userId)
        {
            try
            {
                // Convert int userId to string for use with repository
                string userIdStr = userId.ToString();

                var user = await _userRepository.GetByIdAsync(userIdStr);

                if (user == null)
                {
                    return null;
                }

                // Convert User to UserProfileDto
                return new UserProfileDto
                {
                    Id = user.Id,
                    Email = user.ShowEmail ? user.Email : null,
                    DisplayName = user.DisplayName,
                    Bio = user.Bio,
                    PhoneNumber = user.ShowPhoneNumber ? user.PhoneNumber : null,
                    City = user.City,
                    Province = user.Province,
                    ProfilePicture = user.ProfilePicture,
                    CreatedAt = user.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting profile for user {userId}");
                return null;
            }
        }

        public virtual async Task<AuthResult> IsEmailVerifiedAsync(string userId)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);

                if (user == null)
                {
                    return AuthResult.FailureResult("User not found");
                }

                if (user.IsEmailVerified)
                {
                    return AuthResult.SuccessResult(user);
                }

                return AuthResult.FailureResult("Email not verified");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking email verification for user {userId}");
                return AuthResult.FailureResult("An error occurred while checking email verification status");
            }
        }

        public virtual async Task<AuthResult> GenerateEmailVerificationTokenAsync(User user)
        {
            try
            {
                if (user == null)
                {
                    return AuthResult.FailureResult("User is required");
                }

                if (string.IsNullOrEmpty(user.Id))
                {
                    return AuthResult.FailureResult("User ID is required");
                }

                // Generate token
                string token = GenerateVerificationToken();

                // Create token in repository
                bool success = await CreateVerificationTokenAsync(user.Id, token, VerificationType.EmailVerification);

                if (!success)
                {
                    return AuthResult.FailureResult("Failed to save verification token");
                }

                return AuthResult.SuccessResult(user, token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating verification token for user {user.Id}");
                return AuthResult.FailureResult("An error occurred while generating the email verification token");
            }
        }

        public virtual string GenerateVerificationToken()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] tokenData = new byte[32];
                rng.GetBytes(tokenData);
                return Convert.ToBase64String(tokenData)
                    .Replace('+', '-')
                    .Replace('/', '_')
                    .Replace("=", "");
            }
        }

        public virtual async Task<bool> CreateVerificationTokenAsync(string userId, string token, VerificationType verificationType)
        {
            try
            {
                // Convert int userId to string for use with repository
                string userIdStr = userId.ToString();

                // Check if user exists
                var user = await _userRepository.GetByIdAsync(userIdStr);
                if (user == null)
                {
                    return false;
                }

                // Create token object
                var verificationToken = new VerificationToken
                {
                    UserId = userIdStr,
                    User = user,
                    Token = token,
                    Type = MapVerificationType(verificationType),
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(VERIFICATION_TOKEN_EXPIRY_HOURS),
                    IsUsed = false
                };

                // Save to repository
                return await _userRepository.CreateVerificationTokenAsync(verificationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating verification token for user {userId}");
                return false;
            }
        }

        #region Helper Methods

        // Add a helper method to map between the two VerificationType enums
        protected virtual Models.VerificationType MapVerificationType(VerificationType sourceType)
        {
            return sourceType switch
            {
                VerificationType.EmailVerification => Models.VerificationType.EmailVerification,
                VerificationType.PasswordReset => Models.VerificationType.PasswordReset,
                _ => Models.VerificationType.EmailVerification // Default case
            };
        }

        protected virtual string HashPassword(string password)
        {
            // Generate a random salt
            byte[] salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Hash the password
            byte[] hash = GeneratePasswordHash(password, salt);

            // Combine salt and hash
            byte[] hashBytes = new byte[36];
            Array.Copy(salt, 0, hashBytes, 0, 16);
            Array.Copy(hash, 0, hashBytes, 16, 20);

            // Convert to base64 string
            return Convert.ToBase64String(hashBytes);
        }

        protected virtual bool VerifyPasswordHash(string password, string storedHash)
        {
            try
            {
                // Convert from base64 string
                byte[] hashBytes = Convert.FromBase64String(storedHash);

                // Extract the salt
                byte[] salt = new byte[16];
                Array.Copy(hashBytes, 0, salt, 0, 16);

                // Hash the input password
                byte[] hash = GeneratePasswordHash(password, salt);

                // Compare the results
                for (int i = 0; i < 20; i++)
                {
                    if (hashBytes[i + 16] != hash[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                // If any error occurs, return false
                return false;
            }
        }

        protected virtual byte[] GeneratePasswordHash(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(20);
            }
        }

        #endregion
    }
}