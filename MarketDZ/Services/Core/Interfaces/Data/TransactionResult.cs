using MarketDZ.Services.Core.Interfaces.Data;

namespace MarketDZ.Services.Core.Models
{
    /// <summary>
    /// Implementation of ITransactionResult
    /// </summary>
    /// <typeparam name="T">Result type</typeparam>
    public class TransactionResult<T> : ITransactionResult<T>
    {
        /// <inheritdoc/>
        public bool Success { get; }

        /// <inheritdoc/>
        public T Data { get; }

        /// <inheritdoc/>
        public string ErrorMessage { get; }

        private TransactionResult(bool success, T data, string errorMessage)
        {
            Success = success;
            Data = data;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a successful result
        /// </summary>
        /// <param name="data">Result data</param>
        /// <returns>Successful result</returns>
        public static TransactionResult<T> Successful(T data) =>
            new TransactionResult<T>(true, data, null);

        /// <summary>
        /// Creates a failed result
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        /// <returns>Failed result</returns>
        public static TransactionResult<T> Failed(string errorMessage) =>
            new TransactionResult<T>(false, default, errorMessage);
    }
}