// MarketDZ.Models.Dtos/AuthResult.cs
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Core.Dtos.Auth


{
    /// <summary>
    /// Result of authentication operations
    /// </summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public User? User { get; set; }
        public string? Token { get; set; }

        // Factory methods for easy creation
        public static AuthResult SuccessResult(User? user, string? token = null) =>
            new AuthResult
            {
                Success = true,
                User = user,
                Token = token
            };

        public static AuthResult FailureResult(string message) =>
            new AuthResult
            {
                Success = false,
                ErrorMessage = message
            };
    }
}