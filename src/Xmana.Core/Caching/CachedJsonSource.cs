using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Logging;

namespace Xmana.Core.Caching
{
    public enum DataOrigin
    {
        /// <summary>Fetched from the network just now.</summary>
        Network,

        /// <summary>Served from cache inside its fresh lifetime; no request was made.</summary>
        CacheFresh,

        /// <summary>Served from cache past its fresh lifetime; a background refresh was started.</summary>
        CacheStale,

        /// <summary>The network failed; an older cached copy is shown instead.</summary>
        CacheAfterError,
    }

    public sealed class CachedJson
    {
        public CachedJson(JToken token, DataOrigin origin, DateTime? storedUtc)
        {
            Token = token;
            Origin = origin;
            StoredUtc = storedUtc;
        }

        public JToken Token { get; }

        public DataOrigin Origin { get; }

        /// <summary>When the cached copy was stored (null for fresh network data).</summary>
        public DateTime? StoredUtc { get; }
    }

    /// <summary>
    /// "Cache first + refresh in the background" (PROMPT.md sections 34 and 67).
    /// fresh -> return cache; stale -> return cache and refresh quietly; expired -> network, falling back to an
    /// older copy only when the network fails. Responses are stored sanitised (no signed URLs).
    /// Concurrent refreshes of the same key are merged.
    /// </summary>
    public sealed class CachedJsonSource
    {
        private readonly IResponseCache _cache;
        private readonly CachePolicy _policy;
        private readonly IClock _clock;
        private readonly IXmanaLogger _logger;
        private readonly object _refreshLock = new object();
        private readonly Dictionary<string, Task> _refreshes = new Dictionary<string, Task>(StringComparer.Ordinal);

        public CachedJsonSource(IResponseCache cache, CachePolicy? policy = null, IClock? clock = null, IXmanaLogger? logger = null)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _policy = policy ?? CachePolicy.Default;
            _clock = clock ?? SystemClock.Instance;
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>Raised after a background refresh stored newer data for <c>key</c>.</summary>
        public event EventHandler<string>? BackgroundRefreshed;

        public async Task<CachedJson> GetAsync(
            string key,
            CacheKind kind,
            Func<CancellationToken, Task<JToken>> fetch,
            CancellationToken cancellationToken)
        {
            if (kind == CacheKind.None)
            {
                var direct = await fetch(cancellationToken).ConfigureAwait(false);
                return new CachedJson(direct, DataOrigin.Network, null);
            }

            var rule = _policy.For(kind);
            var cached = await TryReadAsync(key, cancellationToken).ConfigureAwait(false);
            TimeSpan age = TimeSpan.Zero;
            if (cached != null)
            {
                age = _clock.UtcNow - cached.Value.Entry.StoredUtc;
                if (age < TimeSpan.Zero)
                {
                    age = TimeSpan.Zero; // clock moved backwards: treat as brand new rather than trusting forever
                }

                if (age <= rule.Ttl)
                {
                    return new CachedJson(cached.Value.Token, DataOrigin.CacheFresh, cached.Value.Entry.StoredUtc);
                }

                if (age <= rule.Ttl + rule.StaleWindow)
                {
                    StartBackgroundRefresh(key, fetch);
                    return new CachedJson(cached.Value.Token, DataOrigin.CacheStale, cached.Value.Entry.StoredUtc);
                }
            }

            try
            {
                var token = await fetch(cancellationToken).ConfigureAwait(false);
                await StoreAsync(key, token).ConfigureAwait(false);
                return new CachedJson(token, DataOrigin.Network, null);
            }
            catch (ApiException) when (cached != null && age <= rule.ErrorFallback && !cancellationToken.IsCancellationRequested)
            {
                _logger.Warn("Using cached copy after a network failure for " + KeyLabel(key));
                return new CachedJson(cached.Value.Token, DataOrigin.CacheAfterError, cached.Value.Entry.StoredUtc);
            }
        }

        /// <summary>Completes when every started background refresh has finished (used by tests and shutdown).</summary>
        public Task WaitForRefreshesAsync()
        {
            Task[] pending;
            lock (_refreshLock)
            {
                pending = _refreshes.Values.ToArray();
            }

            return Task.WhenAll(pending);
        }

        private void StartBackgroundRefresh(string key, Func<CancellationToken, Task<JToken>> fetch)
        {
            // The lock is held until the task is registered; RunRefreshAsync yields first, so its cleanup (which also
            // takes the lock) can never run before registration. One refresh per key at a time.
            lock (_refreshLock)
            {
                if (_refreshes.ContainsKey(key))
                {
                    return;
                }

                _refreshes[key] = RunRefreshAsync(key, fetch);
            }
        }

        private async Task RunRefreshAsync(string key, Func<CancellationToken, Task<JToken>> fetch)
        {
            await Task.Yield();
            try
            {
                // Not tied to the caller's token: the screen may be gone, but the refreshed data is still useful.
                var token = await fetch(CancellationToken.None).ConfigureAwait(false);
                await StoreAsync(key, token).ConfigureAwait(false);
                BackgroundRefreshed?.Invoke(this, key);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _logger.Warn("Background refresh failed for " + KeyLabel(key) + " (" + ex.GetType().Name + ")");
            }
            finally
            {
                lock (_refreshLock)
                {
                    _refreshes.Remove(key);
                }
            }
        }

        private async Task StoreAsync(string key, JToken token)
        {
            try
            {
                var entry = new CacheEntry(key, CacheSanitizer.ToCacheJson(token), _clock.UtcNow);
                await _cache.SetAsync(entry, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                // A failing disk must never fail a request that already succeeded.
                _logger.Warn("Could not write cache entry for " + KeyLabel(key));
            }
        }

        private async Task<CachedToken?> TryReadAsync(string key, CancellationToken cancellationToken)
        {
            CacheEntry? entry;
            try
            {
                entry = await _cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            if (entry == null)
            {
                return null;
            }

            try
            {
                return new CachedToken(entry, JsonParser.Parse(entry.Json));
            }
            catch (JsonException)
            {
                await _cache.RemoveAsync(key, CancellationToken.None).ConfigureAwait(false);
                return null;
            }
        }

        // Keys are relative API paths; log only the first path segment (the endpoint name), never parameters.
        private static string KeyLabel(string key)
        {
            var end = key.IndexOfAny(new[] { '/', '?' });
            return end > 0 ? key.Substring(0, end) : key;
        }

        private struct CachedToken
        {
            public CachedToken(CacheEntry entry, JToken token)
            {
                Entry = entry;
                Token = token;
            }

            public CacheEntry Entry { get; }

            public JToken Token { get; }
        }
    }
}
