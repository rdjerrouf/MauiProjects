using System.Security.Cryptography;
using MarketDZ.Models;
using MarketDZ.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Infrastructure.Firebase.Implementations
{
    public class FirebaseUserAuthService : UserAuthService
    {
        private readonly IMediaService _mediaService;

        public FirebaseUserAuthService(
            IDbUserRepository userRepository,
            ISecureStorage secureStorage,
            ILogger<FirebaseUserAuthService> logger,
            IEmailService emailService,
            IMediaService mediaService,
            ITemporaryRegistrationStore temporaryRegistrationStore)
            : base(userRepository, secureStorage, emailService, temporaryRegistrationStore, logger)
        {
            _mediaService = mediaService ?? throw new ArgumentNullException(nameof(mediaService));
        }

        // Override methods that require Firebase-specific implementations
        public override async Task<AuthResult> RegisterUserAsync(UserRegistrationDto registrationDto)
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
                    return new AuthResult { Success = false, ErrorMessage = "Email is already in use" };
                }

                var newUser = new User
                {
                    Email = registrationDto.Email,
                    PasswordHash = HashPassword(registrationDto.Password),
                    DisplayName = registrationDto.DisplayName,
                    PhoneNumber = registrationDto.PhoneNumber,
                    City = registrationDto.City,
                    Province = registrationDto.Province,
                    CreatedAt = DateTime.UtcNow,
                    IsEmailVerified = false
                };

                if (registrationDto.ProfilePicture != null)
                {
                    try
                    {
                        var profileImageUrl = await _mediaService.UploadImageAsync(registrationDto.ProfilePicture);
                        if (!string.IsNullOrEmpty(profileImageUrl))
                        {
                            newUser.ProfilePicture = profileImageUrl;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading profile picture during registration");
                    }
                }

                bool created = await _userRepository.CreateAsync(newUser);
                if (!created)
                {
                    return new AuthResult { Success = false, ErrorMessage = "Failed to create user" };
                }

                // Get the created user by email to get the assigned ID
                var createdUser = await _userRepository.GetByEmailAsync(newUser.Email);
                if (createdUser == null)
                {
                    return new AuthResult { Success = false, ErrorMessage = "User created but could not be retrieved" };
                }

                // Use the retrieved user to generate verification token
                var tokenResult = await GenerateEmailVerificationTokenAsync(createdUser);
                if (!tokenResult.Success || string.IsNullOrEmpty(tokenResult.Token))
                {
                    _logger.LogWarning($"Failed to create verification token for user: {createdUser.Id}");
                }
                else
                {
                    string verificationLink = $"marketdz://verify?userId={createdUser.Id}&token={Uri.EscapeDataString(tokenResult.Token)}";
                    await _emailService.SendEmailVerificationAsync(createdUser.Email, verificationLink);
                }

                _logger.LogInformation($"User {createdUser.Email} registered successfully, verification email sent");
                return new AuthResult { Success = true, User = createdUser };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration");
                return new AuthResult { Success = false, ErrorMessage = "An error occurred during registration" };
            }
        }

        // For Firebase, implement direct confirmation instead of using temporary storage
        public override async Task<AuthResult> ConfirmRegistrationAsync(string token)
        {
            // Use the token to verify email directly in this implementation
            return await ConfirmEmailAsync(token);
        }
    }
}