using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    /// <summary>
    /// Cache dependency tracking
    /// </summary>
    public class CacheDependency
    {
        public string? DependentKey { get; set; }
        public required string[] ParentKeys { get; set; }
        public CachePolicy Policy { get; set; }
    }

}
