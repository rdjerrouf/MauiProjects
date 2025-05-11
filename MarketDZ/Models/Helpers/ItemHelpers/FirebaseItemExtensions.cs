using MarketDZ.Models;
using MarketDZ.Models.Core.Entities;

namespace MarketDZ.Models.Helpers.ItemHelpers
{
    public static class FirebaseItemExtensions
    {
        /// <summary>
        /// Converts a FirebaseItem to a domain Item.
        /// Adjust mappings as needed.
        /// </summary>
        public static Item ToItem(this FirebaseItem firebaseItem)
        {
            if (firebaseItem == null) throw new ArgumentNullException(nameof(firebaseItem));

            var item = new Item
            {
                // No need to parse - just assign the string ID directly
                Id = firebaseItem.Id,
                Title = firebaseItem.Title,
                Description = firebaseItem.Description,
                Price = firebaseItem.Price,
                ListedDate = firebaseItem.CreatedAt, // Assuming CreatedAt exists in FirebaseItem
                PhotoUrls = string.IsNullOrEmpty(firebaseItem.PrimaryPhotoUrl)
                                ? new List<string>()
                                : new List<string> { firebaseItem.PrimaryPhotoUrl },
                // Map additional properties as needed
            };
            return item;
        }
    }
}
