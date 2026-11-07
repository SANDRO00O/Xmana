using System;
using System.Collections.Generic;
using System.Linq;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>
    /// Chooses among the qualities the API actually returned for THIS video (never hard-coded). The order of preference is:
    /// the stored preference if present; otherwise a safe default (the best quality up to <see cref="DefaultHeight"/>, else the
    /// lowest available).
    /// </summary>
    public static class QualitySelector
    {
        public const int DefaultHeight = 720;

        public static VideoQuality? Choose(IReadOnlyList<VideoQuality> available, int? preferredHeight)
        {
            if (available == null || available.Count == 0)
            {
                return null;
            }

            var target = preferredHeight ?? DefaultHeight;
            var exact = available.FirstOrDefault(q => q.Height == target);
            if (exact != null)
            {
                return exact;
            }

            var below = available.Where(q => q.Height < target).OrderByDescending(q => q.Height).FirstOrDefault();
            if (below != null)
            {
                return below;
            }

            return available.OrderBy(q => q.Height).First();
        }

        /// <summary>
        /// The next quality to try after <paramref name="failed"/> fail(s): lower qualities first (nearest first, they stream more
        /// easily), then higher ones; heights already tried are skipped. Null when everything has been tried.
        /// </summary>
        public static VideoQuality? Fallback(IReadOnlyList<VideoQuality> available, VideoQuality current, ISet<int> failedHeights)
        {
            var candidates = available.Where(q => q.Height != current.Height && !failedHeights.Contains(q.Height)).ToList();
            var lower = candidates.Where(q => q.Height < current.Height).OrderByDescending(q => q.Height).FirstOrDefault();
            if (lower != null)
            {
                return lower;
            }

            return candidates.Where(q => q.Height > current.Height).OrderBy(q => q.Height).FirstOrDefault();
        }
    }
}
