// Models/BlockedUser.cs
namespace MarketDZ.Models.Core.Entities
{
    public class BlockedUser
    {
        public string?  Id { get; set; }

        public string?  UserId { get; set; }
        public User User { get; set; } = null!;

        public string?  BlockedUserId { get; set; }
        public User BlockedUserProfile { get; set; } = null!;

        public DateTime BlockedAt { get; set; } = DateTime.UtcNow;

        public string? Reason { get; set; }
    }
}