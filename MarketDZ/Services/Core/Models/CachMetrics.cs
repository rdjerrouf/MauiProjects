using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketDZ.Services.Infrastructure.Common.Cache;

namespace MarketDZ.Services.Core.Models
{
    // Helper class for metrics tracking
    public class CacheMetrics
    {
        public string? Key { get; set; }
        public CachePolicy Policy { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public long Hits { get; set; }
        public long Misses { get; set; }
        public long Size { get; set; }
        public double HitRate => Hits + Misses > 0 ? (double)Hits / (Hits + Misses) : 0;
    }

}
