using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Defines a database transaction interface for atomic operations
    /// </summary>
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// Retrieves an entity within the transaction
        /// </summary>
        /// <typeparam name="T">Type of entity to retrieve</typeparam>
        /// <param name="path">Path to the entity</param>
        /// <returns>Entity of type T or default</returns>
        Task<T> GetEntityAsync<T>(string path);

        /// <summary>
        /// Creates or updates an entity within the transaction
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="path">Path where to store the entity</param>
        /// <param name="data">Entity data</param>
        Task SetEntityAsync<T>(string path, T data);

        /// <summary>
        /// Updates specific fields of an entity within the transaction
        /// </summary>
        /// <param name="path">Path to the entity</param>
        /// <param name="updates">Dictionary of field updates</param>
        Task UpdateFieldsAsync(string path, IDictionary<string, object> updates);

        /// <summary>
        /// Deletes an entity within the transaction
        /// </summary>
        /// <param name="path">Path to the entity</param>
        Task DeleteEntityAsync(string path);

        /// <summary>
        /// Commits all changes in the transaction
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Cancels all changes in the transaction
        /// </summary>
        Task RollbackAsync();
    }
}