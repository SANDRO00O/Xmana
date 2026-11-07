using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Models
{
    /// <summary>
    /// Video as it appears in lists (the common subset of the API.md "video object").
    /// Numeric-looking fields are parsed leniently; the original text is kept in the *Raw properties.
    /// </summary>
    public sealed class VideoSummary
    {
        public VideoSummary(
            string id,
            string? enTitle,
            string? arTitle,
            double? stars,
            string? year,
            string? kindRaw,
            VideoKind kind,
            int? season,
            int? episodeNumber,
            string? rootSeriesId,
            string? imageUrl,
            string? thumbUrl,
            string? mediumThumbUrl,
            string? imageObjUrl,
            string? thumbObjUrl,
            string? mediumThumbObjUrl,
            string? trailer,
            IReadOnlyList<CategoryName> categories,
            JObject raw)
        {
            Id = id;
            EnTitle = enTitle;
            ArTitle = arTitle;
            Stars = stars;
            Year = year;
            KindRaw = kindRaw;
            Kind = kind;
            Season = season;
            EpisodeNumber = episodeNumber;
            RootSeriesId = rootSeriesId;
            ImageUrl = imageUrl;
            ThumbUrl = thumbUrl;
            MediumThumbUrl = mediumThumbUrl;
            ImageObjUrl = imageObjUrl;
            ThumbObjUrl = thumbObjUrl;
            MediumThumbObjUrl = mediumThumbObjUrl;
            Trailer = trailer;
            Categories = categories;
            Raw = raw;
        }

        /// <summary>Server <c>nb</c>.</summary>
        public string Id { get; }

        public string? EnTitle { get; }

        public string? ArTitle { get; }

        /// <summary>Server <c>stars</c> ("rating-like value exposed by the API"); null when absent or not numeric.</summary>
        public double? Stars { get; }

        public string? Year { get; }

        public string? KindRaw { get; }

        public VideoKind Kind { get; }

        public int? Season { get; }

        /// <summary>Server <c>episodeNummer</c>.</summary>
        public int? EpisodeNumber { get; }

        /// <summary>Server <c>rootSeries</c>: parent series id when applicable (kept as sent, including "0").</summary>
        public string? RootSeriesId { get; }

        public string? ImageUrl { get; }

        public string? ThumbUrl { get; }

        public string? MediumThumbUrl { get; }

        /// <summary>May carry temporary signed URLs: use for display only, never log or persist.</summary>
        public string? ImageObjUrl { get; }

        public string? ThumbObjUrl { get; }

        public string? MediumThumbObjUrl { get; }

        public string? Trailer { get; }

        public IReadOnlyList<CategoryName> Categories { get; }

        public JObject Raw { get; }

        /// <summary>
        /// Synopsis in the requested language (fallback to the other), read from the documented <c>ar_content</c> /
        /// <c>en_content</c> fields when the list item carries them; null otherwise. Never invented.
        /// </summary>
        public string? GetDescription(bool preferArabic)
        {
            var ar = Xmana.Core.Json.JsonRead.Text(Xmana.Core.Json.JsonRead.Get(Raw, "ar_content"));
            var en = Xmana.Core.Json.JsonRead.Text(Xmana.Core.Json.JsonRead.Get(Raw, "en_content"));
            return preferArabic ? (ar ?? en) : (en ?? ar);
        }

        /// <summary>Title in the requested language with fallback to the other one.</summary>
        public string? GetTitle(bool preferArabic)
        {
            var preferred = preferArabic ? ArTitle : EnTitle;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : (preferArabic ? EnTitle : ArTitle);
        }
    }

    /// <summary>Category as embedded inside a video (API.md: en_title, ar_title).</summary>
    public sealed class CategoryName
    {
        public CategoryName(string? enTitle, string? arTitle)
        {
            EnTitle = enTitle;
            ArTitle = arTitle;
        }

        public string? EnTitle { get; }

        public string? ArTitle { get; }
    }
}
