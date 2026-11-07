using System;
using System.Linq;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>
    /// The unit of <c>skippingDurations.start/end</c>. API.md (section 17 item 4): the unit of populated values was never
    /// established because every tested response was empty. It stays <see cref="Unknown"/> until a populated response is
    /// verified, and while it is unknown no skip control is ever shown.
    /// </summary>
    public enum SkipTimeUnit
    {
        Unknown,
        Seconds,
        Milliseconds,
    }

    public sealed class SkipSegment
    {
        public SkipSegment(TimeSpan start, TimeSpan end)
        {
            Start = start;
            End = end;
        }

        public TimeSpan Start { get; }

        public TimeSpan End { get; }
    }

    /// <summary>Decides whether "Skip Intro" is available at a given position. Nothing is invented: no unit, no data, no flag, no button.</summary>
    public static class SkipController
    {
        /// <summary>
        /// The unit applied to server skip values. Set it (one line) only after verifying a populated response; see
        /// docs/api-notes.md. Until then Skip Intro stays hidden by design.
        /// </summary>
        public static SkipTimeUnit VerifiedUnit { get; set; } = SkipTimeUnit.Unknown;

        private static readonly TimeSpan EndMargin = TimeSpan.FromSeconds(1);

        public static SkipSegment? Active(SkipInfo? info, bool? hasIntroSkipping, SkipTimeUnit unit, TimeSpan position)
        {
            if (info == null || unit == SkipTimeUnit.Unknown || hasIntroSkipping == false || !info.HasUsableRanges)
            {
                return null;
            }

            foreach (var range in info.Ranges.OrderBy(r => r.Start))
            {
                var start = Convert(range.Start, unit);
                var end = Convert(range.End, unit);
                if (end <= start)
                {
                    continue;
                }

                if (position >= start && position < end - EndMargin)
                {
                    return new SkipSegment(start, end);
                }
            }

            return null;
        }

        public static TimeSpan Convert(double value, SkipTimeUnit unit)
        {
            switch (unit)
            {
                case SkipTimeUnit.Seconds:
                    return TimeSpan.FromSeconds(value);
                case SkipTimeUnit.Milliseconds:
                    return TimeSpan.FromMilliseconds(value);
                default:
                    throw new InvalidOperationException("The skip time unit has not been verified.");
            }
        }
    }
}
