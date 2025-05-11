using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    /// <summary>
    /// Defines cache policy levels based on data volatility
    /// </summary>
    public enum CachePolicy
    {
        /// <summary>
        /// For rapidly changing data (e.g., view counts, item statuses)
        /// </summary>
        Volatile,

        /// <summary>
        /// For moderately changing data (e.g., active item listings)
        /// </summary>
        Moderate,

        /// <summary>
        /// For rarely changing data (e.g., categories, states, tags)
        /// </summary>
        Stable,

        /// <summary>
        /// For immutable data (e.g., historical records, archived content)
        /// </summary>
        Immutable
    }


}