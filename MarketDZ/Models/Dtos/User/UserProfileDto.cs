
namespace MarketDZ.Models.Dtos.User
{
    public class UserProfileDto
    {
        public string? Id { get; set; }

        public string? Email { get; set; }

        public string? DisplayName { get; set; }

        public string? ProfilePicture { get; set; }

        public string? Bio { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? PhoneNumber { get; set; }

        public string? City { get; set; }

        public string? Province { get; set; }

        public int PostedItemsCount { get; set; }

        public int FavoriteItemsCount { get; set; }

        public ICollection<FileResult> Photos { get; set; } = new List<FileResult>();
    }

}
