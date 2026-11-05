using Newtonsoft.Json.Linq;
using System;

namespace Xmana.Models
{
    public sealed class VideoItem
    {
        public string Id { get; set; }
        public string EnglishTitle { get; set; }
        public string ArabicTitle { get; set; }
        public string OtherTitle { get; set; }
        public string Year { get; set; }
        public string Kind { get; set; }
        public string Season { get; set; }
        public string EpisodeNumber { get; set; }
        public string DescriptionEn { get; set; }
        public string DescriptionAr { get; set; }
        public string Poster { get; set; }
        public string Backdrop { get; set; }
        public string Rating { get; set; }
        public string Duration { get; set; }
        public string RootSeries { get; set; }

        public string GetTitle(bool arabic)
        {
            var preferred = arabic ? ArabicTitle : EnglishTitle;
            return !String.IsNullOrWhiteSpace(preferred) ? preferred :
                (!String.IsNullOrWhiteSpace(OtherTitle) ? OtherTitle : (Id ?? "Xmana"));
        }

        public string GetDescription(bool arabic)
        {
            var preferred = arabic ? DescriptionAr : DescriptionEn;
            return !String.IsNullOrWhiteSpace(preferred) ? preferred : (DescriptionEn ?? DescriptionAr ?? String.Empty);
        }

        public static VideoItem FromToken(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            return new VideoItem
            {
                Id = Read(token, "nb", "id", "videoId"),
                EnglishTitle = Read(token, "en_title", "enTitle", "title"),
                ArabicTitle = Read(token, "ar_title", "arTitle"),
                OtherTitle = Read(token, "other_title", "otherTitle"),
                Year = Read(token, "year", "mDate"),
                Kind = Read(token, "kind"),
                Season = Read(token, "season"),
                EpisodeNumber = Read(token, "episodeNummer", "episodeNumber"),
                DescriptionEn = Read(token, "en_content", "description", "overview"),
                DescriptionAr = Read(token, "ar_content"),
                Poster = Read(token, "imgMediumThumbObjUrl", "imgObjUrl", "imgMediumThumb", "imgThumbObjUrl", "imgThumb", "img"),
                Backdrop = Read(token, "imgObjUrl", "img"),
                Rating = Read(token, "filmRating", "seriesRating", "stars", "rate"),
                Duration = Read(token, "duration"),
                RootSeries = Read(token, "rootSeries")
            };
        }

        private static string Read(JToken token, params string[] names)
        {
            foreach (var name in names)
            {
                var value = token[name];
                if (value == null || value.Type == JTokenType.Null ||
                    value.Type == JTokenType.Object || value.Type == JTokenType.Array) continue;
                var text = value.ToString();
                if (!String.IsNullOrWhiteSpace(text)) return text;
            }
            return String.Empty;
        }
    }
}
