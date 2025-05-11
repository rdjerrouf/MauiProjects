namespace MarketDZ.Models.Core.Infrastructure
{
    /// <summary>
    /// Interface for entities that support versioning for optimistic concurrency control
    /// </summary>
    public interface IVersionedEntity
    {
        /// <summary>
        /// Gets or sets the version number of the entity
        /// </summary>
        int Version { get; set; }

        /// <summary>
        /// Gets or sets the last modified date of the entity
        /// </summary>
        DateTime LastModified { get; set; }
    }
}