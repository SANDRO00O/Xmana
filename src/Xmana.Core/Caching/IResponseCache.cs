using System;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Caching
{
    /// <summary>One cached response: the (sanitised) JSON text and when it was stored.</summary>
    public sealed class CacheEntry
    {
        public CacheEntry(string key, string json, DateTime storedUtc)
        {
            Key = key;
            Json = json;
            StoredUtc = storedUtc;
        }

        public string Key { get; }

        public string Json { get; }

        public DateTime StoredUtc { get; }
    }

    public interface IResponseCache
    {
        Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken);

        Task SetAsync(CacheEntry entry, CancellationToken cancellationToken);

        Task RemoveAsync(string key, CancellationToken cancellationToken);

        Task ClearAsync(CancellationToken cancellationToken);
    }
}
