using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Interface for transaction helper operations
    /// </summary>
    public interface ITransactionHelper
    {
        /// <summary>
        /// Executes a function in a transaction
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="entityPath">Path of the main entity</param>
        /// <param name="updateFunction">Function that updates the entity and returns related updates</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated entity</returns>
        Task<T> ExecuteTransactionAsync<T>(
            string entityPath,
            Func<T, Dictionary<string, object>, (T newValue, Dictionary<string, object> updates)> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Executes multiple updates in a batch
        /// </summary>
        /// <param name="updates">Dictionary of paths and values to update</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful</returns>
        Task<bool> ExecuteBatchOperationAsync(
            Dictionary<string, object> updates,
            int maxRetries = 3,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets and updates an entity atomically
        /// </summary>
        /// <typeparam name="T">Entity type</typeparam>
        /// <param name="entityPath">Path of the entity</param>
        /// <param name="updateFunction">Function that updates the entity</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Updated entity</returns>
        Task<T> AtomicGetAndUpdateAsync<T>(
            string entityPath,
            Func<T, T> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Increments a numeric field atomically
        /// </summary>
        /// <param name="entityPath">Path of the entity</param>
        /// <param name="fieldPath">Path of the field to increment</param>
        /// <param name="amount">Amount to increment</param>
        /// <param name="maxRetries">Maximum number of retries</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if successful</returns>
        Task<bool> AtomicIncrementAsync(
            string entityPath,
            string fieldPath,
            double amount = 1,
            int maxRetries = 3,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a custom transaction operation
        /// </summary>
        /// <typeparam name="T">Result type</typeparam>
        /// <param name="operation">Operation to execute</param>
        /// <returns>Transaction result</returns>
        Task<ITransactionResult<T>> ExecuteAtomicOperationAsync<T>(
            Func<ITransactionContext, Task<T>> operation);
    }
}