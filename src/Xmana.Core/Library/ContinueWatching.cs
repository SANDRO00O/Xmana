using System;
using System.Collections.Generic;
using System.Linq;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Core.Library
{
    /// <summary>A title with the viewer's local progress, for Continue Watching and History.</summary>
    public sealed class ProgressItem
    {
        public ProgressItem(VideoSummary video, PlaybackProgress progress)
        {
            Video = video;
            Progress = progress;
        }

        public VideoSummary Video { get; }

        public PlaybackProgress Progress { get; }

        /// <summary>0..1 for the progress bar (completed titles are full).</summary>
        public double Fraction =>
            Progress.Completed ? 1 : Progress.Duration > TimeSpan.Zero ? Math.Min(1, Math.Max(0, Progress.Position.TotalSeconds / Progress.Duration.TotalSeconds)) : 0;
    }

    public static class ContinueWatching
    {
        public const int DefaultMax = 15;

        /// <summary>
        /// Titles worth continuing: a meaningful, unfinished position, newest first. For a series only the most recently watched episode
        /// is offered. Entries whose title is unknown locally are left out (they cannot be shown).
        /// </summary>
        public static IReadOnlyList<ProgressItem> Build(IEnumerable<PlaybackProgress> recent, Func<string, VideoSummary?> snapshot, int max = DefaultMax)
        {
            var result = new List<ProgressItem>();
            var seenSeries = new HashSet<string>(StringComparer.Ordinal);
            foreach (var progress in recent.OrderByDescending(p => p.LastPlayedUtc))
            {
                if (!ProgressPolicy.IsResumable(progress))
                {
                    // A newer finished or too-short entry of a series still hides that series' older episodes.
                    if (progress.SeriesId != null)
                    {
                        seenSeries.Add(progress.SeriesId);
                    }

                    continue;
                }

                if (progress.SeriesId != null && !seenSeries.Add(progress.SeriesId))
                {
                    continue;
                }

                var video = snapshot(progress.VideoId);
                if (video == null)
                {
                    continue;
                }

                result.Add(new ProgressItem(video, progress));
                if (result.Count >= max)
                {
                    break;
                }
            }

            return result;
        }
    }

    public static class WatchHistory
    {
        /// <summary>Everything watched (finished or not), newest first, for titles that are known locally.</summary>
        public static IReadOnlyList<ProgressItem> Build(IEnumerable<PlaybackProgress> recent, Func<string, VideoSummary?> snapshot)
        {
            var result = new List<ProgressItem>();
            foreach (var progress in recent.OrderByDescending(p => p.LastPlayedUtc))
            {
                var video = snapshot(progress.VideoId);
                if (video != null)
                {
                    result.Add(new ProgressItem(video, progress));
                }
            }

            return result;
        }
    }
}
