// MarketDZ.Models.Infrastructure.Firebase.Mappers/ItemPhotoEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using System;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class ItemPhotoEntityMapper : IEntityMapper<ItemPhoto, FirebaseItemPhoto>
    {
        private readonly IEntityMapper<Item, FirebaseItem> _itemMapper;

        public ItemPhotoEntityMapper(IEntityMapper<Item, FirebaseItem> itemMapper)
        {
            _itemMapper = itemMapper;
        }

        public ItemPhoto ToDomain(FirebaseItemPhoto entity)
        {
            if (entity == null) return null!;

            // Use the existing conversion method from your FirebaseItemPhoto class
            return entity.ToItemPhoto();
        }

        public FirebaseItemPhoto ToEntity(ItemPhoto domain)
        {
            if (domain == null) return null!;

            // Ensure domain.Id is not null by providing a fallback value
            var id = domain.Id ?? string.Empty;

            // Use the existing conversion method from your FirebaseItemPhoto class
            return FirebaseItemPhoto.FromItemPhoto(domain, id);
        }
    }
}