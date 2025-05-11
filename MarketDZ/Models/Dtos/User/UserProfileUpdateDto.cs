using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Dtos.User
{
    /// <summary>
    /// Data Transfer Object for updating a user's profile
    /// </summary>
    public class UserProfileUpdateDto
    {
        public string? DisplayName { get; set; }

        public string? Bio { get; set; }

        public string? PhoneNumber { get; set; }

        public string? City { get; set; }

        public string? Province { get; set; }

        public AlState? State { get; set; }

        public FileResult? ProfilePicture { get; set; }

        public string? ProfilePictureUrl { get; set; }

        // Optional: Password change fields
        public string? CurrentPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}