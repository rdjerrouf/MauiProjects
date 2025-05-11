using MarketDZ.Models.Firebase.Base;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    public interface IFirebaseTransactionHelper
    {
        Task<T> ExecuteTransactionAsync<T>(
            string entityPath,
            Func<T, Dictionary<string, object>, (T newValue, Dictionary<string, object> updates)> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class;

        Task<bool> ExecuteBatchOperationAsync(
            Dictionary<string, object> updates,
            int maxRetries = 3,
            CancellationToken cancellationToken = default);

        Task<T> AtomicGetAndUpdateAsync<T>(
            string entityPath,
            Func<T, T> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class;

        Task<bool> AtomicIncrementAsync(
            string entityPath,
            string fieldPath,
            double amount = 1,
            int maxRetries = 3,
            CancellationToken cancellationToken = default);

        // Add missing method needed by FirebaseItemPhotoRepository
        Task<TransactionResult<T>> ExecuteAtomicOperationAsync<T>(
            Func<ITransactionContext, Task<T>> operation);
    }

    public interface ITransactionContext
    {
        Task<T> GetEntityAsync<T>(string path);
        Task UpdateEntityAsync<T>(string path, T entity);
        Task DeleteEntityAsync(string path);
    }

    public class TransactionContext : ITransactionContext
    {
        private readonly ITransaction _transaction;

        public TransactionContext(ITransaction transaction)
        {
            _transaction = transaction;
        }

        public Task<T> GetEntityAsync<T>(string path) => _transaction.GetEntityAsync<T>(path);
        public Task UpdateEntityAsync<T>(string path, T entity) => _transaction.SetEntityAsync(path, entity);
        public Task DeleteEntityAsync(string path) => _transaction.DeleteEntityAsync(path);
    }

    public class TransactionResult<T>
    {
        public bool Success { get; }
        public T Data { get; }
        public string ErrorMessage { get; }

        private TransactionResult(bool success, T data, string errorMessage)
        {
            Success = success;
            Data = data;
            ErrorMessage = errorMessage;
        }

        public static TransactionResult<T> Successful(T data) =>
            new TransactionResult<T>(true, data, null!);

        public static TransactionResult<T> Failed(string errorMessage) =>
            new TransactionResult<T>(false, default!, errorMessage);
    }

    public class FirebaseTransactionHelper : IFirebaseTransactionHelper
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseTransactionHelper> _logger;

        public FirebaseTransactionHelper(IAppCoreDataStore dataStore, ILogger<FirebaseTransactionHelper> logger)
        {
            _dataStore = dataStore;
            _logger = logger;
        }

        public async Task<T> ExecuteTransactionAsync<T>(
            string entityPath,
            Func<T, Dictionary<string, object>, (T newValue, Dictionary<string, object> updates)> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class
        {
            int attempt = 0;
            while (attempt < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    using var transaction = await _dataStore.BeginTransactionAsync();

                    // Get current value
                    var currentValue = await transaction.GetEntityAsync<T>(entityPath);
                    var additionalData = new Dictionary<string, object>();

                    // Execute update function
                    var (newValue, updates) = updateFunction(currentValue, additionalData);

                    // Apply updates
                    await transaction.SetEntityAsync(entityPath, newValue);

                    // Apply any additional updates
                    foreach (var update in updates)
                    {
                        await transaction.SetEntityAsync(update.Key, update.Value);
                    }

                    // Commit transaction
                    await transaction.CommitAsync();
                    return newValue;
                }
                catch (ConcurrencyException ex)
                {
                    _logger.LogWarning(ex, $"Concurrency conflict in transaction (attempt {attempt}/{maxRetries})");
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for transaction");
                        throw;
                    }

                    await Task.Delay(CalculateRetryDelay(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Transaction failed");
                    throw;
                }
            }

            throw new InvalidOperationException("Transaction failed after maximum retries");
        }

        public async Task<bool> ExecuteBatchOperationAsync(
            Dictionary<string, object> updates,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            while (attempt < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    await _dataStore.BatchUpdateAsync(updates);
                    return true;
                }
                catch (ConcurrencyException ex)
                {
                    _logger.LogWarning(ex, $"Batch operation conflict (attempt {attempt}/{maxRetries})");
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for batch operation");
                        throw;
                    }

                    await Task.Delay(CalculateRetryDelay(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch operation failed");
                    throw;
                }
            }

            return false;
        }

        public async Task<T> AtomicGetAndUpdateAsync<T>(
            string entityPath,
            Func<T, T> updateFunction,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class
        {
            return await ExecuteTransactionAsync<T>(
                entityPath,
                (current, _) =>
                {
                    var newValue = updateFunction(current);
                    return (newValue, new Dictionary<string, object>());
                },
                maxRetries,
                cancellationToken);
        }

        public async Task<bool> AtomicIncrementAsync(
            string entityPath,
            string fieldPath,
            double amount = 1,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            while (attempt < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;

                try
                {
                    using var transaction = await _dataStore.BeginTransactionAsync();

                    // Get current entity
                    var entity = await transaction.GetEntityAsync<Dictionary<string, object>>(entityPath);
                    if (entity == null)
                    {
                        _logger.LogWarning($"Entity not found at path: {entityPath}");
                        return false;
                    }

                    // Get current value
                    if (!entity.TryGetValue(fieldPath, out var currentValueObj))
                    {
                        currentValueObj = 0;
                    }

                    // Parse current value
                    if (!double.TryParse(currentValueObj.ToString(), out double currentValue))
                    {
                        currentValue = 0;
                    }

                    // Update value
                    entity[fieldPath] = currentValue + amount;

                    // Update version if exists
                    if (entity.ContainsKey("version"))
                    {
                        entity["version"] = Convert.ToInt32(entity["version"]) + 1;
                    }

                    // Update last modified timestamp
                    entity["lastModified"] = DateTime.UtcNow;

                    // Save changes
                    await transaction.SetEntityAsync(entityPath, entity);
                    await transaction.CommitAsync();
                    return true;
                }
                catch (ConcurrencyException ex)
                {
                    _logger.LogWarning(ex, $"Increment conflict (attempt {attempt}/{maxRetries})");
                    if (attempt >= maxRetries)
                    {
                        _logger.LogError(ex, "Max retries reached for increment operation");
                        throw;
                    }

                    await Task.Delay(CalculateRetryDelay(attempt), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Increment operation failed");
                    throw;
                }
            }

            return false;
        }

        // Add the implementation of ExecuteAtomicOperationAsync
        public async Task<TransactionResult<T>> ExecuteAtomicOperationAsync<T>(
            Func<ITransactionContext, Task<T>> operation)
        {
            int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var transaction = await _dataStore.BeginTransactionAsync();
                    var context = new TransactionContext(transaction);

                    var result = await operation(context);
                    await transaction.CommitAsync();

                    return TransactionResult<T>.Successful(result);
                }
                catch (ConcurrencyException ex) when (attempt < maxRetries - 1)
                {
                    _logger.LogWarning(ex, $"Concurrency conflict on attempt {attempt + 1}, retrying...");
                    await Task.Delay(CalculateRetryDelay(attempt));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing atomic operation");
                    return TransactionResult<T>.Failed(ex.Message);
                }
            }

            return TransactionResult<T>.Failed("Maximum retry attempts exceeded");
        }

        #region Standard Transaction Patterns

        public async Task<bool> UpdateWithVersionCheckAsync<T>(
            string entityPath,
            T updatedEntity,
            int expectedVersion,
            int maxRetries = 3,
            CancellationToken cancellationToken = default) where T : class, IVersionedEntity
        {
            return await ExecuteTransactionAsync<T>(
                entityPath,
                (current, _) =>
                {
                    if (current == null)
                        throw new InvalidOperationException("Entity not found");

                    if (current.Version != expectedVersion)
                        throw new ConcurrencyException("Version mismatch");

                    updatedEntity.Version = current.Version + 1;
                    updatedEntity.LastModified = DateTime.UtcNow;

                    return (updatedEntity, new Dictionary<string, object>());
                },
                maxRetries,
                cancellationToken) != null;
        }

        public async Task<bool> TransferValueAsync(
            string fromPath,
            string toPath,
            string fieldName,
            double amount,
            int maxRetries = 3,
            CancellationToken cancellationToken = default)
        {
            return await ExecuteTransactionAsync<Dictionary<string, object>>(
                fromPath,
                (fromEntity, _) =>
                {
                    using var innerTransaction = _dataStore.BeginTransactionAsync().Result;

                    // Get 'to' entity
                    var toEntity = innerTransaction.GetEntityAsync<Dictionary<string, object>>(toPath).Result;

                    // Validate balances
                    double fromBalance = Convert.ToDouble(fromEntity[fieldName]);
                    if (fromBalance < amount)
                        throw new InvalidOperationException("Insufficient balance");

                    double toBalance = Convert.ToDouble(toEntity[fieldName]);

                    // Update balances
                    fromEntity[fieldName] = fromBalance - amount;
                    toEntity[fieldName] = toBalance + amount;

                    // Update versions
                    if (fromEntity.ContainsKey("version"))
                        fromEntity["version"] = Convert.ToInt32(fromEntity["version"]) + 1;
                    if (toEntity.ContainsKey("version"))
                        toEntity["version"] = Convert.ToInt32(toEntity["version"]) + 1;

                    // Prepare updates
                    var updates = new Dictionary<string, object>
                    {
                        [fromPath] = fromEntity,
                        [toPath] = toEntity
                    };

                    return (fromEntity, updates);
                },
                maxRetries,
                cancellationToken) != null;
        }

        #endregion

        #region Private Helpers

        private TimeSpan CalculateRetryDelay(int attempt)
        {
            // Exponential backoff with jitter
            var baseDelay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
            var jitter = new Random().Next(0, 50);
            return baseDelay.Add(TimeSpan.FromMilliseconds(jitter));
        }

        #endregion
    }

    public class ConcurrencyException : Exception
    {
        public ConcurrencyException(string message) : base(message) { }
        public ConcurrencyException(string message, Exception inner) : base(message, inner) { }
    }
}
