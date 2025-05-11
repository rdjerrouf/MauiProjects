using System.ComponentModel.DataAnnotations;
using MarketDZ.Converters;
using MarketDZ.Models.Core.Infrastructure;

namespace MarketDZ.Models.Core.Entities
{

    public class User : IEntity
    {
        public string Id { get; set; } = string.Empty;
        public string? EmailVerificationToken { get; set; }

        [Required]
        public required string Email { get; set; }

        [Required]
        public required string PasswordHash { get; set; }

        public string?DisplayName { get; set; }
        public string? ProfilePicture { get; set; }
        public string? Bio { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? PhoneNumber { get; set; }
        public string? City { get; set; }
        public string? Province { get; set; }
        public bool ShowEmail { get; set; }
        public bool ShowPhoneNumber { get; set; }

        // Missing properties referenced in code
        public bool IsEmailVerified { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }

        public bool IsAdmin { get; set; }
        // Navigation properties
        public ICollection<Item> PostedItems { get; set; } = new List<Item>();
        public ICollection<Item> FavoriteItems { get; set; } = new List<Item>();
        public string? Name { get; internal set; }
        public AlState State { get; internal set; }
        public bool ShowPhone { get; internal set; }
    }
}