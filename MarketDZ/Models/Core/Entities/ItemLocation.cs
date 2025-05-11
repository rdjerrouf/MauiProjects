
namespace MarketDZ.Models.Core.Entities
{
    public class ItemLocation
    {
        public string?  Id { get; set; }
        public string?  ItemId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? LocationName { get; set; }

        // Navigation property
        public Item? Item { get; set; }

        // Helper method to convert to Location object
        public Location ToLocation()
        {
            return new Location(Latitude, Longitude);
        }

        // Create from a Location object
        public static ItemLocation FromLocation(Location location, string?  itemId, string? locationName = null)
        {
            return new ItemLocation
            {
                ItemId = itemId,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                LocationName = locationName
            };
        }
    }
}