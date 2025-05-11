
namespace MarketDZ.Models.Core.Entities
{
    public enum ItemStatus
    {
        Active = 0,     // Item is currently available
        Sold = 1,       // Item has been sold
        Rented = 2,     // Item has been rented
        Unavailable = 3 // Item is no longer available
    }
}
