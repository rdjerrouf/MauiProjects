using System;

namespace MarketDZ.Services.Core.Models
{
    /// <summary>
    /// Represents filter operation types for queries
    /// </summary>
    public enum FilterOperator
    {
        Equal,
        NotEqual,
        GreaterThan,
        GreaterThanOrEqual,
        LessThan,
        LessThanOrEqual,
        Contains,
        StartsWith,
        EndsWith,
        In,
        NotIn,
        Between,
        Exists
    }
}