using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Caching
{
    /// <summary>Small in-memory cache. When over capacity the oldest quarter is dropped.</summary>
    public sealed class MemoryResponseCache : IResponseCache
    {
        private readonly ConcurrentDictionary<string, CacheEntry> _entries = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);
        private readonly int _maxEntries;

        public MemoryResponseCache(int maxEntries = 256)
        {
            if (maxEntries < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntries));
            }

            _maxEntries = maxEntries;
        }

        public int Count => _entries.Count;

        public Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken)
        {
            CacheEntry? entry;
            return Task.FromResult(_entries.TryGetValue(key, out entry) ? entry : null);
        }

        public Task SetAsync(CacheEntry entry, CancellationToken cancellationToken)
        {
            _entries[entry.Key] = entry;
            if (_entries.Count > _maxEntries)
            {
                var drop = _entries.Values.OrderBy(e => e.StoredUtc).Take(Math.Max(1, _maxEntries / 4)).ToList();
                foreach (var old in drop)
                {
                    CacheEntry removed;
                    _entries.TryRemove(old.Key, out removed);
                }
            }

            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            CacheEntry removed;
            _entries.TryRemove(key, out removed);
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            _entries.Clear();
            return Task.CompletedTask;
        }
    }
}
