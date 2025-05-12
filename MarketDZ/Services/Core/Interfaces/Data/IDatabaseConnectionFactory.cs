using System;
using System.Threading.Tasks;

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Interface for creating database connections
    /// </summary>
    /// <typeparam name="TConnection">Database-specific connection type</typeparam>
    public interface IDatabaseConnectionFactory<TConnection>
    {
        /// <summary>
        /// Creates a new connection
        /// </summary>
        /// <returns>Database connection</returns>
        Task<TConnection> CreateConnectionAsync();

        /// <summary>
        /// Gets a connection from the pool or creates a new one
        /// </summary>
        /// <returns>Database connection</returns>
        Task<TConnection> GetConnectionAsync();

        /// <summary>
        /// Returns a connection to the pool
        /// </summary>
        /// <param name="connection">Connection to return</param>
        Task ReleaseConnectionAsync(TConnection connection);

        /// <summary>
        /// Validates a connection
        /// </summary>
        /// <param name="connection">Connection to validate</param>
        /// <returns>True if the connection is valid</returns>
        Task<bool> ValidateConnectionAsync(TConnection connection);
    }
}