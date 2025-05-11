
namespace MarketDZ.Models.Core.Entities
{
    public class ItemStatistics
    {
        public string?  Id { get; set; }
        public string?  ItemId { get; set; }
        public Item? Item { get; set; }
        public int ViewCount { get; set; } = 0;
        public int InquiryCount { get; set; } = 0;
        public int FavoriteCount { get; set; } = 0;
        public DateTime? FirstViewedAt { get; set; }
        public DateTime? LastViewedAt { get; set; }
        public TimeSpan TotalTimeOnMarket { get; set; }
        public int DaysListed { get; set; }
        public double AverageRating { get; set; }
        public int RatingCount { get; set; }
        public List<Rating> Ratings { get; set; } = new List<Rating>();
    }
}
