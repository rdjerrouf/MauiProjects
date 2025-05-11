// MarketDZ.Models.Infrastructure.Firebase.Mappers/FavoriteEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class FavoriteEntityMapper : IEntityMapper<UserFavorite, FirebaseFavorite>
    {
        public UserFavorite ToDomain(FirebaseFavorite entity)
        {
            if (entity == null) return null!;

            // Use the existing conversion method
            return entity.ToUserFavorite();
        }

        public FirebaseFavorite ToEntity(UserFavorite domain)
        {
            if (domain == null) return null!;

            // Use the existing conversion method
            return FirebaseFavorite.FromUserFavorite(domain, domain.Id);
        }
    }
}