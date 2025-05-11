using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketDZ.Services.Infrastructure.Common.Cache
{
    /// <summary>
    /// Enhanced cache item with metrics and dependencies
    /// </summary>
    public class EnhancedCacheItemWithMetrics<T>
    {
        // The actual value (only stored if not compressed)
        private T _value;

        // Compressed data (only stored if compression is used)
        private byte[] _compressedData;

        public DateTime ExpiresAt { get; }
        public bool IsCompressed { get; }
        public DateTime LastAccessed { get; private set; }
        public int AccessCount { get; private set; }
        public HashSet<string> Dependencies { get; set; } = new HashSet<string>();
        public CachePolicy Policy { get; set; }
        public DateTime CreatedAt { get; set; }
        public long TotalAccessCount { get; set; }
        public long Size { get; set; }
        public double HitRate => AccessCount > 0 ? (double)AccessCount / (AccessCount + 1) : 0;

        // Create a non-compressed cache item
        public EnhancedCacheItemWithMetrics(T value, TimeSpan expiration, bool compress = false, CachePolicy policy = CachePolicy.Moderate)
        {
            if (compress)
            {
                _value = default;
                _compressedData = CompressObject(value);
                IsCompressed = true;
            }
            else
            {
                _value = value;
                _compressedData = null;
                IsCompressed = false;
            }

            ExpiresAt = DateTime.UtcNow.Add(expiration);
            LastAccessed = DateTime.UtcNow;
            AccessCount = 0;
            Policy = policy;
            CreatedAt = DateTime.UtcNow;
            Size = EstimateSize(value);
        }

        public bool IsExpired => DateTime.UtcNow > ExpiresAt;

        public T GetValue()
        {
            LastAccessed = DateTime.UtcNow;
            AccessCount++;
            TotalAccessCount++;

            if (IsCompressed)
            {
                return DecompressObject<T>(_compressedData);
            }

            return _value;
        }

        // Compress an object to a byte array
        private byte[] CompressObject<TObj>(TObj obj)
        {
            var jsonData = System.Text.Json.JsonSerializer.Serialize(obj);
            var rawData = Encoding.UTF8.GetBytes(jsonData);

            using var memory = new MemoryStream();
            using (var gzip = new GZipStream(memory, CompressionLevel.Optimal))
            {
                gzip.Write(rawData, 0, rawData.Length);
            }

            return memory.ToArray();
        }

        // Decompress a byte array to an object
        private TObj DecompressObject<TObj>(byte[] compressedData)
        {
            using var memory = new MemoryStream(compressedData);
            using var outputMemory = new MemoryStream();
            using (var gzip = new GZipStream(memory, CompressionMode.Decompress))
            {
                gzip.CopyTo(outputMemory);
            }

            var rawData = outputMemory.ToArray();
            var jsonData = Encoding.UTF8.GetString(rawData);
            return System.Text.Json.JsonSerializer.Deserialize<TObj>(jsonData);
        }

        private long EstimateSize(T value)
        {
            try
            {
                return System.Text.Json.JsonSerializer.Serialize(value).Length;
            }
            catch
            {
                return 1000; // Default estimate
            }
        }
    }
}