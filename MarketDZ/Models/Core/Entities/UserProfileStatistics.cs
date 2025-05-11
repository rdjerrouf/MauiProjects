namespace MarketDZ.Models.Core.Entities
{
    public class UserProfileStatistics
    {
        public string?  UserId { get; set; }
        public int PostedItemsCount { get; set; }
        public int FavoriteItemsCount { get; set; }
        public double AverageRating { get; set; }
        public int ActiveListings { get; set; }
        public int TotalViews { get; set; }
        public int TotalListings { get; set; }
        public DateTime JoinedDate { get; set; }
        public List<Rating> RecentRatings { get; set; } = new List<Rating>();
    }
}
