// MarketDZ.Models.Infrastructure.Firebase.Mappers/ItemEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;
using System;
using System.Linq;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class ItemEntityMapper : IEntityMapper<Item, FirebaseItem>
    {
        private readonly IEntityMapper<User, FirebaseUser> _userMapper;

        public ItemEntityMapper(IEntityMapper<User, FirebaseUser> userMapper)
        {
            _userMapper = userMapper;
        }

        public Item ToDomain(FirebaseItem entity)
        {
            if (entity == null) return null!;

            // Use the existing conversion method from your FirebaseItem class
            return entity.ToDomainModel();
        }

        public FirebaseItem ToEntity(Item domain)
        {
            if (domain == null) return null!;

            // Use the existing conversion method from your FirebaseItem class
            return FirebaseItem.FromItem(domain, domain.Id);
        }
    }
}