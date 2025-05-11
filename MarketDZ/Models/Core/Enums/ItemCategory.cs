// ItemCategory.cs - Fix the enum values
namespace MarketDZ.Models.Core.Entities
{
    /// <summary>
    /// Defines the main categories of items in the marketplace
    /// </summary>
    public enum ItemCategory
    {
        ForSale,   // Not "For Sale" (with a space)
        ForRent,   // Not "For Rent" (with a space)
        Job,       // Not "Jobs" (plural)
        Service    // Not "Services" (plural)
    }
}