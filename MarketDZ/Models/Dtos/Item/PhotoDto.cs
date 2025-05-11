namespace MarketDZ.Models.Dtos.Item
{
    public class PhotoDto
    {
        public string?  PhotoId { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
    }
}