using System;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Models;

namespace Xmana.Core.Library
{
    /// <summary>
    /// A compact local copy of what is needed to show a title in Favorites, Watchlist, History and Continue Watching without any
    /// network request. Only plain fields are kept: signed <c>*ObjUrl</c> addresses are never stored (PROMPT.md section 34).
    /// </summary>
    public sealed class LibraryItem
    {
        public LibraryItem(
            string id,
            string? titleEn,
            string? titleAr,
            string? year,
            string? kind,
            double? stars,
            string? image,
            string? imageThumb,
            string? imageMediumThumb,
            string? rootSeries,
            int? season,
            int? episode,
            DateTime seenUtc)
        {
            Id = id;
            TitleEn = titleEn;
            TitleAr = titleAr;
            Year = year;
            Kind = kind;
            Stars = stars;
            Image = image;
            ImageThumb = imageThumb;
            ImageMediumThumb = imageMediumThumb;
            RootSeries = rootSeries;
            Season = season;
            Episode = episode;
            SeenUtc = seenUtc;
        }

        public string Id { get; }

        public string? TitleEn { get; }

        public string? TitleAr { get; }

        public string? Year { get; }

        public string? Kind { get; }

        public double? Stars { get; }

        public string? Image { get; }

        public string? ImageThumb { get; }

        public string? ImageMediumThumb { get; }

        public string? RootSeries { get; }

        public int? Season { get; }

        public int? Episode { get; }

        /// <summary>When the snapshot was last refreshed (used to drop the oldest first).</summary>
        public DateTime SeenUtc { get; }

        public static LibraryItem From(VideoSummary video, DateTime nowUtc) =>
            new LibraryItem(
                video.Id,
                video.EnTitle,
                video.ArTitle,
                video.Year,
                video.KindRaw,
                video.Stars,
                video.ImageUrl,
                video.ThumbUrl,
                video.MediumThumbUrl,
                video.RootSeriesId,
                video.Season,
                video.EpisodeNumber,
                nowUtc);

        /// <summary>Rebuilds a list item through the normal mapper (so every consumer sees the same model as for live data).</summary>
        public VideoSummary ToSummary()
        {
            var json = new JObject { { "nb", Id } };
            Set(json, "en_title", TitleEn);
            Set(json, "ar_title", TitleAr);
            Set(json, "year", Year);
            Set(json, "kind", Kind);
            Set(json, "img", Image);
            Set(json, "imgThumb", ImageThumb);
            Set(json, "imgMediumThumb", ImageMediumThumb);
            Set(json, "rootSeries", RootSeries);
            if (Stars.HasValue)
            {
                json["stars"] = Stars.Value;
            }

            if (Season.HasValue)
            {
                json["season"] = Season.Value;
            }

            if (Episode.HasValue)
            {
                json["episodeNummer"] = Episode.Value;
            }

            return VideoMapper.ToSummary(json)!;
        }

        /// <summary>True when everything shown for the title is identical (so a refresh need not rewrite the file).</summary>
        public bool SameContent(LibraryItem other) =>
            other != null
            && Id == other.Id
            && TitleEn == other.TitleEn
            && TitleAr == other.TitleAr
            && Year == other.Year
            && Kind == other.Kind
            && Stars == other.Stars
            && Image == other.Image
            && ImageThumb == other.ImageThumb
            && ImageMediumThumb == other.ImageMediumThumb
            && RootSeries == other.RootSeries
            && Season == other.Season
            && Episode == other.Episode;

        private static void Set(JObject json, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                json[name] = value;
            }
        }
    }
}
