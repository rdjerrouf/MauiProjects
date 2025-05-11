



namespace MarketDZ.Models.Core
{
    /// <summary>
    /// Data Transfer Object for user registration
    /// </summary>
    public class UserRegistrationDto
    {
        public required string Email { get; set; }

        public required string Password { get; set; }

        public required string DisplayName { get; set; }

        public string? PhoneNumber { get; set; }

        public string? City { get; set; }

        public string? Province { get; set; }

        public AlState? State { get; set; }

        public bool AgreeToTerms { get; set; }

        public FileResult? ProfilePicture { get; set; }
    }
}