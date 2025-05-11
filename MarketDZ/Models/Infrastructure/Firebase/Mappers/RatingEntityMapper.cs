// MarketDZ.Models.Infrastructure.Firebase.Mappers/RatingEntityMapper.cs
using MarketDZ.Models.Core.Entities;
using MarketDZ.Models.Core.Infrastructure;
using MarketDZ.Models.Infrastructure.Firebase.Entities;

namespace MarketDZ.Models.Infrastructure.Firebase.Mappers
{
    public class RatingEntityMapper : IEntityMapper<Rating, FirebaseRating>
    {
        public Rating ToDomain(FirebaseRating entity)
        {
            if (entity == null) return null;

            // Use the existing conversion method
            return entity.ToRating();
        }

        public FirebaseRating ToEntity(Rating domain)
        {
            if (domain == null) return null;

            // Use the existing conversion method
            return FirebaseRating.FromRating(domain, domain.Id);
        }
    }
}