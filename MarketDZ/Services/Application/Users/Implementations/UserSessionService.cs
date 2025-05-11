using MarketDZ.Services.UserServices;
using MarketDZ.Models; 
using Microsoft.Extensions.Logging;


namespace MarketDZ.Services.Application.Users.Implementations
{
    public class UserSessionService : IUserSessionService
    {
        private readonly IUserProfileService _userProfileService;
        private readonly ILogger<UserSessionService> _logger;
        private User? _currentUser;

        public UserSessionService(
            IUserProfileService userProfileService,
            ILogger<UserSessionService> logger)
        {
            _userProfileService = userProfileService ?? throw new ArgumentNullException(nameof(userProfileService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public User? CurrentUser => _currentUser;

        public bool IsLoggedIn => _currentUser != null;

        public void SetCurrentUser(User? user)
        {
            _currentUser = user ?? throw new ArgumentNullException(nameof(user));
        }

        public void ClearCurrentUser()
        {
            _currentUser = null;
            // Clear the stored user ID from secure storage
            Task.Run(async () =>
            {
                try
                {
                    await SecureStorage.SetAsync("userId", _currentUser.Id.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error clearing user session");
                }
            });
        }

        public async Task SaveSessionAsync()
        {
            if (_currentUser != null)
            {
                try
                {
                    if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
                    {
                        await SecureStorage.SetAsync("userId", _currentUser.Id.ToString());
                        _logger.LogInformation($"Saved user ID {_currentUser.Id} to secure storage");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving user session");
                }
            }
        }

        public async Task<bool> RestoreSessionAsync()
        {
            try
            {
                string? storedUserId = await SecureStorage.GetAsync("userId");

                if (string.IsNullOrEmpty(storedUserId))
                {
                    _logger.LogInformation("No stored user ID found");
                    return false;
                }

                // Convert the storedUserId to string as required by GetUserByIdAsync
                var user = await _userProfileService.GetUserByIdAsync(storedUserId);

                if (user == null)
                {
                    _logger.LogWarning($"User with ID {storedUserId} not found in database");
                    await SecureStorage.SetAsync("userId", string.Empty);
                    return false;
                }

                _currentUser = user;
                _logger.LogInformation($"Successfully restored session for user: {user.Email}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring session");
                return false;
            }
        }
    }

}
