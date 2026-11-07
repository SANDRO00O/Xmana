using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Models
{
    /// <summary>
    /// A playable quality. <see cref="Url"/> is a temporary, signed URL: keep it in memory only, never log or persist it.
    /// <see cref="ToString"/> deliberately omits it.
    /// </summary>
    public sealed class VideoQuality
    {
        public VideoQuality(string label, int height, string url)
        {
            Label = label;
            Height = height;
            Url = url;
        }

        /// <summary>Quality label as the server names it, e.g. "720p".</summary>
        public string Label { get; }

        public int Height { get; }

        public string Url { get; }

        public override string ToString() => Label;
    }

    public enum SubtitleLanguage
    {
        Arabic,
        English,
        Spanish,
    }

    public sealed class SubtitleTrack
    {
        public SubtitleTrack(SubtitleLanguage language, string file)
        {
            Language = language;
            File = file;
        }

        public SubtitleLanguage Language { get; }

        /// <summary>The server's subtitle file reference (may be a temporary URL): never log or persist it.</summary>
        public string File { get; }

        /// <summary>
        /// The track's address when the server supplied an absolute HTTPS URL. API.md does not say whether subtitle files are
        /// URLs or relative paths, so a value that is not an absolute HTTPS URL is treated as unusable and the track is hidden.
        /// </summary>
        public bool TryGetUri(out System.Uri uri) => Xmana.Core.Http.UrlValidator.TryGetHttpsUri(File, out uri);

        public override string ToString() => Language.ToString();
    }

    /// <summary>Result of <c>translationFiles</c>. Only tracks with a non-empty file are listed as available.</summary>
    public sealed class TranslationInfo
    {
        public TranslationInfo(IReadOnlyList<SubtitleTrack> tracks, string? arPath, string? enPath, JObject raw)
        {
            Tracks = tracks;
            ArPath = arPath;
            EnPath = enPath;
            Raw = raw;
        }

        public IReadOnlyList<SubtitleTrack> Tracks { get; }

        public bool HasSubtitles => Tracks.Count > 0;

        /// <summary>Server <c>arTranslationFilePath</c> (the tested response used a placeholder image path when none exists).</summary>
        public string? ArPath { get; }

        public string? EnPath { get; }

        public JObject Raw { get; }
    }

    /// <summary>One skip range as the server delivered it (start[i], end[i]); the time unit is not documented, so no conversion is applied.</summary>
    public sealed class SkipRange
    {
        public SkipRange(double start, double end)
        {
            Start = start;
            End = end;
        }

        public double Start { get; }

        public double End { get; }
    }

    public sealed class SkipInfo
    {
        public SkipInfo(IReadOnlyList<SkipRange> ranges, int startCount, int endCount, JToken raw)
        {
            Ranges = ranges;
            StartCount = startCount;
            EndCount = endCount;
            Raw = raw;
        }

        /// <summary>Only complete, ordered pairs (start &lt; end). Empty means: do not show Skip Intro.</summary>
        public IReadOnlyList<SkipRange> Ranges { get; }

        public int StartCount { get; }

        public int EndCount { get; }

        public bool HasUsableRanges => Ranges.Count > 0;

        public JToken Raw { get; }
    }

    /// <summary>Result of <c>video_thumbnails</c>. Empty (status 204) is a normal outcome.</summary>
    public sealed class ThumbnailInfo
    {
        public ThumbnailInfo(int? status, IReadOnlyList<string> urls, int itemCount, JToken raw)
        {
            Status = status;
            Urls = urls;
            ItemCount = itemCount;
            Raw = raw;
        }

        public int? Status { get; }

        public IReadOnlyList<string> Urls { get; }

        public int ItemCount { get; }

        public bool HasThumbnails => Urls.Count > 0;

        public JToken Raw { get; }
    }

    /// <summary>
    /// Result of <c>checkVideoParentalLevel</c>. <see cref="Status"/> is exactly what the backend sent;
    /// no meaning is attached to any value (API.md: mapping not established).
    /// </summary>
    public sealed class ParentalStatus
    {
        public ParentalStatus(string? status, JToken raw)
        {
            Status = status;
            Raw = raw;
        }

        public string? Status { get; }

        public JToken Raw { get; }
    }

    /// <summary>Public comment. The populated schema is unverified (API.md), so only the raw object is exposed.</summary>
    public sealed class VideoComment
    {
        public VideoComment(JObject raw)
        {
            Raw = raw;
        }

        public JObject Raw { get; }
    }

    /// <summary>Result of the public rating-summary service. Show a rating only when <see cref="HasData"/> is true.</summary>
    public sealed class RatingSummary
    {
        public RatingSummary(IReadOnlyList<JToken> items, int? totalCount, JToken raw)
        {
            Items = items;
            TotalCount = totalCount;
            Raw = raw;
        }

        public IReadOnlyList<JToken> Items { get; }

        public int? TotalCount { get; }

        public bool HasData => Items.Count > 0 || (TotalCount ?? 0) > 0;

        public JToken Raw { get; }
    }
}
