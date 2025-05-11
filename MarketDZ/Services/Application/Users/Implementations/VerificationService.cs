using MarketDZ.Services.DbServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Google.Apis.Util.Store;
using MarketDZ.Models;
using Microsoft.Extensions.Logging;

namespace MarketDZ.Services.Application.Users.Implementations
{
    public class VerificationService : IVerificationService
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly IUserProfileService _userProfileService;
        private readonly ILogger<VerificationService> _logger;

        public VerificationService(
            IAppCoreDataStore dataStore,
            IUserProfileService userProfileService,
            ILogger<VerificationService> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string> GenerateVerificationTokenAsync(string userId, VerificationType type)
        {
            try
            {
                // Generate a random token
                string token = GenerateRandomToken();

                // Get the user first
                var user = await _userProfileService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    throw new Exception($"User with ID {userId} not found");
                }

                // Create and store the verification token
                var verificationToken = new VerificationToken
                {
                    UserId = userId,
                    User = user,
                    Token = token,
                    Type = (Models.VerificationType)type,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Token valid for one day
                    IsUsed = false
                };

                // Save to data store
                object savedTokenEntity = await _dataStore.SetEntityAsync($"verificationTokens/{token}", verificationToken);
                return token;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating verification token for user {userId}");
                throw;
            }
        }

        public async Task<bool> ValidateVerificationTokenAsync(string token, VerificationType type)
        {
            try
            {
                var verificationToken = await _dataStore.GetEntityAsync<VerificationToken>($"verificationTokens/{token}");

                if (verificationToken == null)
                {
                    _logger.LogWarning("Token not found");
                    return false;
                }

                if (verificationToken.Type != (Models.VerificationType)type) // Explicitly cast 'type' to 'Models.VerificationType'
                {
                    _logger.LogWarning($"Token type mismatch. Expected: {type}, Actual: {verificationToken.Type}");
                    return false;
                }

                if (verificationToken.IsUsed)
                {
                    _logger.LogWarning("Token has already been used");
                    return false;
                }

                if (verificationToken.ExpiresAt < DateTime.UtcNow)
                {
                    _logger.LogWarning("Token has expired");
                    return false;
                }

                _logger.LogInformation("Token validated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating verification token: {token}");
                return false;
            }
        }

        public async Task<bool> MarkTokenAsUsedAsync(string token)
        {
            try
            {
                var verificationToken = await _dataStore.GetEntityAsync<VerificationToken>($"verificationTokens/{token}");

                if (verificationToken == null)
                {
                    _logger.LogWarning("Token not found");
                    return false;
                }

                var updates = new Dictionary<string, object> { { "IsUsed", true } };
                await _dataStore.UpdateEntityFieldsAsync($"verificationTokens/{token}", updates);

                _logger.LogInformation("Token marked as used");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error marking token as used: {token}");
                return false;
            }
        }

        private string GenerateRandomToken()
        {
            // Generate a random 32-byte token
            using (var rng = RandomNumberGenerator.Create())
            {
                var tokenBytes = new byte[32];
                rng.GetBytes(tokenBytes);
                return Convert.ToBase64String(tokenBytes)
                    .Replace("/", "_")
                    .Replace("+", "-")
                    .Replace("=", "");
            }
        }
    }
}
