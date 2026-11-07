using System;

namespace Xmana.Core.Playback
{
    /// <summary>When progress counts as meaningful, completed, or worth resuming (PROMPT.md section 43).</summary>
    public static class ProgressPolicy
    {
        /// <summary>Below this a saved position is not worth a Resume prompt.</summary>
        public static readonly TimeSpan MinimumResume = TimeSpan.FromSeconds(15);

        /// <summary>Resume a little before the saved position so the viewer regains context.</summary>
        public static readonly TimeSpan Rewind = TimeSpan.FromSeconds(3);

        /// <summary>Minimum time between periodic progress writes while playing (never per position event).</summary>
        public static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(15);

        private const double CompletedFraction = 0.95;
        private static readonly TimeSpan CompletedRemaining = TimeSpan.FromSeconds(20);

        /// <summary>True when the position is at (or effectively at) the end. Unknown durations are never "completed".</summary>
        public static bool IsCompleted(TimeSpan position, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return false;
            }

            return position.TotalSeconds >= duration.TotalSeconds * CompletedFraction || duration - position <= CompletedRemaining;
        }

        public static bool IsResumable(PlaybackProgress? progress)
        {
            if (progress == null || progress.Completed || progress.Position < MinimumResume)
            {
                return false;
            }

            return !IsCompleted(progress.Position, progress.Duration);
        }

        public static TimeSpan ResumePosition(PlaybackProgress progress)
        {
            var position = progress.Position - Rewind;
            return position < TimeSpan.Zero ? TimeSpan.Zero : position;
        }
    }
}
