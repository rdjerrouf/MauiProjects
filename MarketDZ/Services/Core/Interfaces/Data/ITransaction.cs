// File: Services.Core.Interfaces.Data.ITransaction.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Interface for database transactions that ensure atomic operations
    /// </summary>
    public interface ITransaction : IDisposable
    {
        /// <summary>
        /// Retrieves an entity within the transaction
        /// </summary>
        /// <typeparam name="T">Type of entity to retrieve</typeparam>
        /// <param name="path">Path or identifier of the entity</param>
        /// <returns>The entity or null if not found</returns>
        Task<T> GetEntityAsync<T>(string path) where T : class;

        /// <summary>
        /// Creates or updates an entity within the transaction
        /// </summary>
        /// <typeparam name="T">Type of entity</typeparam>
        /// <param name="path">Path or identifier where the entity should be stored</param>
        /// <param name="data">Entity data</param>
        Task SetEntityAsync<T>(string path, T data) where T : class;

        /// <summary>
        /// Updates specific fields of an entity within the transaction
        /// </summary>
        /// <param name="path">Path or identifier of the entity</param>
        /// <param name="updates">Dictionary of field names and new values</param>
        Task UpdateFieldsAsync(string path, IDictionary<string, object> updates);

        /// <summary>
        /// Deletes an entity within the transaction
        /// </summary>
        /// <param name="path">Path or identifier of the entity to delete</param>
        Task DeleteEntityAsync(string path);

        /// <summary>
        /// Commits all changes in the transaction
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back all changes in the transaction
        /// </summary>
        Task RollbackAsync();

        /// <summary>
        /// Gets the transaction status
        /// </summary>
        TransactionStatus Status { get; }
    }

    /// <summary>
    /// Represents the current status of a transaction
    /// </summary>
    public enum TransactionStatus
    {
        /// <summary>
        /// Transaction is active and changes can be made
        /// </summary>
        Active,

        /// <summary>
        /// Transaction has been committed
        /// </summary>
        Committed,

        /// <summary>
        /// Transaction has been rolled back
        /// </summary>
        RolledBack,

        /// <summary>
        /// Transaction has failed
        /// </summary>
        Failed
    }
}