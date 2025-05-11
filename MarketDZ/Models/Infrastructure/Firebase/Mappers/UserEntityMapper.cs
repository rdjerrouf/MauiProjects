// MarketDZ.Models.Infrastructure.Firebase.Mappers/UserEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class UserEntityMapper : IEntityMapper<User, FirebaseUser>
    {
        public User ToDomain(FirebaseUser entity)
        {
            if (entity == null) return null;

            // Use the existing conversion method
            return entity.ToUser();
        }

        public FirebaseUser ToEntity(User domain)
        {
            if (domain == null) return null;

            // Use the existing conversion method
            return FirebaseUser.FromUser(domain, domain.Id);
        }
    }
}