// File: Services.Core.Interfaces.Data.IAppCoreDataStore.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Core interface for data storage operations. Implementation will be provided 
    /// by specific database providers (Firebase, MongoDB, SQL Server, etc.)
    /// </summary>
    public interface IAppCoreDataStore : IDisposable
    {
        /// <summary>
        /// Initializes the data store connection
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Retrieves an entity by its path
        /// </summary>
        /// <typeparam name="T">Type of entity to retrieve</typeparam>
        /// <param name="path">Path or identifier of the entity</param>
        /// <returns>The entity or null if not found</returns>
        Task<T> GetEntityAsync<T>(string path) where T : class;

        /// <summary>
        /// Retrieves a collection of entities
        /// </summary>
        /// <typeparam name="T">Type of entities to retrieve</typeparam>
        /// <param name="path">Path or identifier of the collection</param>
        /// <param name="parameters">Optional query parameters for filtering, sorting, and pagination</param>
        /// <returns>Collection of entities</returns>
        Task<IReadOnlyCollection<T>> GetCollectionAsync<T>(string path, IQueryParameters parameters = null) where T : class;

        /// <summary>
        /// Creates or updates an entity
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="path">Path or identifier where the entity should be stored</param>
        /// <param name="data">Entity data</param>
        /// <returns>The created or updated entity</returns>
        Task<T> SetEntityAsync<T>(string path, T data) where T : class;

        /// <summary>
        /// Adds a new entity to a collection with an auto-generated identifier
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="path">Path or identifier of the collection</param>
        /// <param name="data">Entity data</param>
        /// <returns>Tuple containing the generated key and the entity</returns>
        Task<(string Key, T Entity)> AddEntityAsync<T>(string path, T data) where T : class;

        /// <summary>
        /// Updates specific fields of an entity
        /// </summary>
        /// <param name="path">Path or identifier of the entity</param>
        /// <param name="updates">Dictionary of field names and new values</param>
        Task UpdateEntityFieldsAsync(string path, IDictionary<string, object> updates);

        /// <summary>
        /// Deletes an entity
        /// </summary>
        /// <param name="path">Path or identifier of the entity to delete</param>
        Task DeleteEntityAsync(string path);

        /// <summary>
        /// Performs multiple update operations in a single batch
        /// </summary>
        /// <param name="updates">Dictionary of paths/identifiers and values to update</param>
        Task BatchUpdateAsync(Dictionary<string, object> updates);

        /// <summary>
        /// Performs multiple delete operations in a single batch
        /// </summary>
        /// <param name="paths">Collection of paths/identifiers to delete</param>
        Task BatchDeleteAsync(IEnumerable<string> paths);

        /// <summary>
        /// Gets the number of items in a collection
        /// </summary>
        /// <param name="path">Path or identifier of the collection</param>
        /// <returns>Number of items</returns>
        Task<int> GetCollectionSizeAsync(string path);

        /// <summary>
        /// Begins a transaction for atomic operations
        /// </summary>
        /// <returns>Transaction interface</returns>
        Task<ITransaction> BeginTransactionAsync();
    }
}