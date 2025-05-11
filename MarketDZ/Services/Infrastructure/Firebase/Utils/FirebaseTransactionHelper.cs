using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Services.DbServices;
using Microsoft.Extensions.Logging;
using static MarketDZ.Services.DbServices.Firebase.FirebaseTransaction;

// FirebaseTransactionHelper.cs
namespace MarketDZ.Services.Infrastructure.Firebase.Utils
{
    public class FirebaseTransactionHelper
    {
        private readonly IAppCoreDataStore _dataStore;
        private readonly ILogger<FirebaseTransactionHelper> _logger;
        private readonly int _maxRetries = 3;
        private readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(100);

        public FirebaseTransactionHelper(IAppCoreDataStore dataStore, ILogger<FirebaseTransactionHelper> logger)
        {
            _dataStore = dataStore ?? throw new ArgumentNullException(nameof(dataStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<TransactionResult<T>> ExecuteAtomicOperationAsync<T>(
            Func<ITransactionContext, Task<T>> operation)
        {
            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    using var transaction = await _dataStore.BeginTransactionAsync();
                    var context = new TransactionContext(transaction);

                    var result = await operation(context);
                    await transaction.CommitAsync();

                    return TransactionResult<T>.Successful(result);
                }
                catch (ConcurrencyException ex) when (attempt < _maxRetries - 1)
                {
                    _logger.LogWarning(ex, $"Concurrency conflict on attempt {attempt + 1}, retrying...");
                    await Task.Delay(_retryDelay * (attempt + 1));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error executing atomic operation");
                    return TransactionResult<T>.Failed(ex.Message);
                }
            }

            return TransactionResult<T>.Failed("Maximum retry attempts exceeded");
        }
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
            new TransactionResult<T>(true, data, null);

        public static TransactionResult<T> Failed(string errorMessage) =>
            new TransactionResult<T>(false, default, errorMessage);
    }
}