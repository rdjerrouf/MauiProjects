using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// File: Services.Core.Interfaces.Data.IQueryBuilder.cs

namespace MarketDZ.Services.Core.Interfaces.Data
{
    /// <summary>
    /// Interface for building database-agnostic queries
    /// </summary>
    /// <typeparam name="TQuery">Database-specific query type</typeparam>
    public interface IQueryBuilder<TQuery>
    {
        /// <summary>
        /// Builds a query from query parameters
        /// </summary>
        /// <param name="baseQuery">Base query to start with</param>
        /// <param name="path">Collection path or name</param>
        /// <param name="parameters">Query parameters</param>
        /// <returns>Built query</returns>
        TQuery BuildQuery(TQuery baseQuery, string path, IQueryParameters parameters);

        /// <summary>
        /// Applies a filter condition to a query
        /// </summary>
        /// <param name="query">Query to modify</param>
        /// <param name="field">Field name</param>
        /// <param name="operator">Operator</param>
        /// <param name="value">Value</param>
        /// <returns>Modified query</returns>
        TQuery ApplyFilter(TQuery query, string field, FilterOperator @operator, object value);

        /// <summary>
        /// Applies sorting to a query
        /// </summary>
        /// <param name="query">Query to modify</param>
        /// <param name="field">Field name</param>
        /// <param name="direction">Sort direction</param>
        /// <returns>Modified query</returns>
        TQuery ApplySort(TQuery query, string field, SortDirection direction);

        /// <summary>
        /// Applies pagination to a query
        /// </summary>
        /// <param name="query">Query to modify</param>
        /// <param name="skip">Number of items to skip</param>
        /// <param name="take">Maximum number of items to take</param>
        /// <returns>Modified query</returns>
        TQuery ApplyPagination(TQuery query, int skip, int take);
    }
}