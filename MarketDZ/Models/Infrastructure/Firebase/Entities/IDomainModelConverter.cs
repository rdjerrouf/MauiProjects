namespace MarketDZ.Models.Infrastructure.Firebase.Entities
{
    /// <summary>
    /// Interface for converting between Firebase models and domain models
    /// </summary>
    /// <typeparam name="T">The domain model type</typeparam>
    public interface IDomainModelConverter<T>
    {
        /// <summary>
        /// Converts the Firebase model to a domain model
        /// </summary>
        /// <returns>Domain model</returns>
        T ToDomainModel();
    }
}