
namespace MarketDZ.Models.Core.Entities
{
    public class Rating
    {
        public string?  Id { get; set; }
        public string?  UserId { get; set; }
        public string?  ItemId { get; set; }
        public int Score { get; set; }
        public string? Review { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public Item Item { get; set; } = null!;
        public bool IsVerifiedPurchase { get; set; } = false;
        public int? HelpfulVotes { get; set; } = 0;

    }
}
