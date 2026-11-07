using System;
using System.Collections.Generic;

namespace Xmana.Core.Caching
{
    /// <summary>What kind of data a request returns; decides its cache lifetime (PROMPT.md sections 34 and 67).</summary>
    public enum CacheKind
    {
        /// <summary>Never cached. Used for signed/temporary data (media files, subtitles) and per-request state.</summary>
        None,
        Banner,
        Categories,
        Groups,
        Featured,
        Latest,
        Popular,
        Listing,
        VideoMetadata,
        Related,
        Search,
        Staff,
        Seasons,
        Episodes,
        Faq,
    }

    /// <summary>
    /// Lifetime of one kind of cached response.
    /// Fresh for <see cref="Ttl"/>; then served immediately while a background refresh runs for
    /// <see cref="StaleWindow"/>; after that the network is required. If the network fails, an entry
    /// no older than <see cref="ErrorFallback"/> may still be shown. Nothing is trusted forever.
    /// </summary>
    public sealed class CacheRule
    {
        public CacheRule(TimeSpan ttl, TimeSpan staleWindow, TimeSpan errorFallback)
        {
            Ttl = ttl;
            StaleWindow = staleWindow;
            ErrorFallback = errorFallback < ttl + staleWindow ? ttl + staleWindow : errorFallback;
        }

        public TimeSpan Ttl { get; }

        public TimeSpan StaleWindow { get; }

        public TimeSpan ErrorFallback { get; }
    }

    public sealed class CachePolicy
    {
        private readonly Dictionary<CacheKind, CacheRule> _rules;

        public CachePolicy(Dictionary<CacheKind, CacheRule> rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        public static CachePolicy Default { get; } = CreateDefault();

        public CacheRule For(CacheKind kind)
        {
            CacheRule? rule;
            return _rules.TryGetValue(kind, out rule) ? rule : Fallback;
        }

        private static readonly CacheRule Fallback = new CacheRule(TimeSpan.FromMinutes(10), TimeSpan.Zero, TimeSpan.FromMinutes(10));

        private static CachePolicy CreateDefault()
        {
            Func<double, double, double, CacheRule> hours = (ttl, stale, fallback) =>
                new CacheRule(TimeSpan.FromHours(ttl), TimeSpan.FromHours(stale), TimeSpan.FromHours(fallback));

            return new CachePolicy(new Dictionary<CacheKind, CacheRule>
            {
                { CacheKind.Banner, hours(0.25, 6, 72) },
                { CacheKind.Categories, hours(12, 168, 720) },
                { CacheKind.Groups, hours(0.25, 6, 72) },
                { CacheKind.Featured, hours(0.5, 12, 72) },
                { CacheKind.Latest, hours(0.25, 6, 72) },
                { CacheKind.Popular, hours(0.5, 12, 72) },
                { CacheKind.Listing, hours(0.25, 6, 72) },
                { CacheKind.VideoMetadata, hours(6, 72, 336) },
                { CacheKind.Related, hours(6, 72, 336) },
                { CacheKind.Search, hours(0.25, 1, 24) },
                { CacheKind.Staff, hours(24, 168, 720) },
                { CacheKind.Seasons, hours(1, 12, 168) },
                { CacheKind.Episodes, hours(1, 12, 168) },
                { CacheKind.Faq, hours(24, 168, 720) },
            });
        }
    }
}
