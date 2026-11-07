using System;
using Xmana.Core.Models;

namespace Xmana.Core.Api
{
    /// <summary>The two <c>targetType</c> values API.md documents for the public rating-summary service.</summary>
    public enum RatingTargetType
    {
        Movie,
        Episode,
    }

    /// <summary>
    /// One rating-summary target. The mapping is the documented frontend logic (API.md section 14):
    /// <c>kind == "2"</c> gives <c>Episode</c>, anything else <c>Movie</c>; the id is <c>rootSeries</c> when it is
    /// present and not "0", otherwise the video's own <c>nb</c>.
    /// </summary>
    public sealed class RatingTarget
    {
        public RatingTarget(string targetId, RatingTargetType targetType)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                throw new ArgumentException("A target id is required.", nameof(targetId));
            }

            TargetId = targetId.Trim();
            TargetType = targetType;
        }

        public string TargetId { get; }

        public RatingTargetType TargetType { get; }

        /// <summary>The exact string sent on the wire.</summary>
        public string TargetTypeName => TargetType == RatingTargetType.Episode ? "Episode" : "Movie";

        public static RatingTarget For(string? kind, string? rootSeriesId, string videoId)
        {
            var type = string.Equals(kind?.Trim(), "2", StringComparison.Ordinal) ? RatingTargetType.Episode : RatingTargetType.Movie;
            var root = rootSeriesId?.Trim();
            var id = !string.IsNullOrEmpty(root) && root != "0" ? root! : videoId;
            return new RatingTarget(id!, type);
        }

        public static RatingTarget For(VideoSummary video)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            return For(video.KindRaw, video.RootSeriesId, video.Id);
        }
    }
}
