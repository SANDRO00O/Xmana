using System;
using System.Collections.Generic;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    public enum PlayerState
    {
        /// <summary>Nothing started.</summary>
        Idle,

        /// <summary>Loading qualities, subtitles, skip data and the saved position.</summary>
        Preparing,

        /// <summary>Prepared and waiting for the user (Resume / Start Over) or for <c>Start</c>.</summary>
        Ready,

        /// <summary>The engine is opening a stream (also while recovering from a failure).</summary>
        Opening,

        Playing,
        Paused,
        Ended,
        Failed,
    }

    public enum PlayerFailure
    {
        None,

        /// <summary>No playable stream could be found for this video.</summary>
        NoSource,

        Network,

        /// <summary>The stream (or its container/codec) is not playable.</summary>
        InvalidMedia,

        Decoding,
        Unknown,
    }

    public enum PlaybackStartMode
    {
        /// <summary>Start at the beginning.</summary>
        StartOver,

        /// <summary>Continue from the saved position (falls back to the beginning when none is meaningful).</summary>
        Resume,
    }

    /// <summary>What the player needs to know about the video being played (built from the details metadata).</summary>
    public sealed class PlaybackContext
    {
        public PlaybackContext(
            string videoId,
            string? titleEn,
            string? titleAr,
            string? seriesId,
            int? season,
            int? episode,
            VideoSummary? previous,
            VideoSummary? next,
            IReadOnlyList<VideoSummary> seasonEpisodes,
            bool? hasIntroSkipping,
            VideoSummary? summary = null)
        {
            Summary = summary;
            VideoId = videoId;
            TitleEn = titleEn;
            TitleAr = titleAr;
            SeriesId = seriesId;
            Season = season;
            Episode = episode;
            Previous = previous;
            Next = next;
            SeasonEpisodes = seasonEpisodes;
            HasIntroSkipping = hasIntroSkipping;
        }

        public string VideoId { get; }

        /// <summary>The video's own list-style summary (titles, plain image addresses), used to remember it in the local library.</summary>
        public VideoSummary? Summary { get; }

        public string? TitleEn { get; }

        public string? TitleAr { get; }

        public string? SeriesId { get; }

        public int? Season { get; }

        public int? Episode { get; }

        public VideoSummary? Previous { get; }

        public VideoSummary? Next { get; }

        /// <summary>Episodes of the season being watched, for the in-player episode list (empty for movies).</summary>
        public IReadOnlyList<VideoSummary> SeasonEpisodes { get; }

        /// <summary>The server's <c>hasIntroSkipping</c> flag; false hides Skip Intro even when timing data exists.</summary>
        public bool? HasIntroSkipping { get; }

        public bool IsEpisode => SeriesId != null;

        public string? GetTitle(bool preferArabic)
        {
            var preferred = preferArabic ? TitleAr : TitleEn;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : (preferArabic ? TitleEn : TitleAr);
        }
    }

    /// <summary>A subtitle track the engine can load. The address is a temporary URL: never logged or persisted.</summary>
    public sealed class SubtitleSource
    {
        public SubtitleSource(SubtitleLanguage language, Uri uri)
        {
            Language = language;
            Uri = uri;
        }

        public SubtitleLanguage Language { get; }

        public Uri Uri { get; }

        public override string ToString() => Language.ToString();
    }

    /// <summary>A playable stream plus subtitle tracks. <see cref="ToString"/> never reveals an address.</summary>
    public sealed class PlaybackSource
    {
        public PlaybackSource(Uri media, string qualityLabel, IReadOnlyList<SubtitleSource> subtitles)
        {
            Media = media;
            QualityLabel = qualityLabel;
            Subtitles = subtitles;
        }

        public Uri Media { get; }

        public string QualityLabel { get; }

        public IReadOnlyList<SubtitleSource> Subtitles { get; }

        public override string ToString() => QualityLabel;
    }

    /// <summary>Locally stored progress of one video (PROMPT.md section 43; docs/playback.md).</summary>
    public sealed class PlaybackProgress
    {
        public PlaybackProgress(string videoId, string? seriesId, int? season, int? episode, TimeSpan position, TimeSpan duration, DateTime lastPlayedUtc, bool completed)
        {
            VideoId = videoId;
            SeriesId = seriesId;
            Season = season;
            Episode = episode;
            Position = position;
            Duration = duration;
            LastPlayedUtc = lastPlayedUtc;
            Completed = completed;
        }

        public string VideoId { get; }

        public string? SeriesId { get; }

        public int? Season { get; }

        public int? Episode { get; }

        public TimeSpan Position { get; }

        public TimeSpan Duration { get; }

        public DateTime LastPlayedUtc { get; }

        public bool Completed { get; }
    }
}
