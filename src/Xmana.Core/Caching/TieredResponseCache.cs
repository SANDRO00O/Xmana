using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Caching
{
    /// <summary>Memory in front of disk: reads hit memory first and promote disk hits; writes go to both.</summary>
    public sealed class TieredResponseCache : IResponseCache
    {
        private readonly IResponseCache _fast;
        private readonly IResponseCache _slow;

        public TieredResponseCache(IResponseCache fast, IResponseCache slow)
        {
            _fast = fast;
            _slow = slow;
        }

        public async Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken)
        {
            var entry = await _fast.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry != null)
            {
                return entry;
            }

            entry = await _slow.GetAsync(key, cancellationToken).ConfigureAwait(false);
            if (entry != null)
            {
                await _fast.SetAsync(entry, cancellationToken).ConfigureAwait(false);
            }

            return entry;
        }

        public async Task SetAsync(CacheEntry entry, CancellationToken cancellationToken)
        {
            await _fast.SetAsync(entry, cancellationToken).ConfigureAwait(false);
            await _slow.SetAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            await _fast.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
            await _slow.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            await _fast.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _slow.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
