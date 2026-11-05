using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

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
        public string ImdbReference { get; set; }
        public string Trailer { get; set; }
        public string VideoViews { get; set; }
        public string VideoLikes { get; set; }
        public string VideoDislikes { get; set; }
        public string ParentSkipping { get; set; }
        public string ArabicSubtitleFile { get; set; }
        public string EnglishSubtitleFile { get; set; }
        public string SpanishSubtitleFile { get; set; }
        public bool IsSpecial { get; set; }
        public bool HasIntroSkipping { get; set; }
        public bool ShowComments { get; set; }
        public bool IsSeries { get; set; }
        public double ResumeSeconds { get; set; }
        public double PlaybackProgressPercent { get; set; }
        public VideoLanguage Language { get; set; }
        public List<CategoryInfo> Categories { get; set; } = new List<CategoryInfo>();
        public List<StaffItem> Actors { get; set; } = new List<StaffItem>();
        public List<StaffItem> Directors { get; set; } = new List<StaffItem>();
        public List<StaffItem> Writers { get; set; } = new List<StaffItem>();

        public string GetTitle(bool arabic)
        {
            var preferred = arabic ? ArabicTitle : EnglishTitle;
            return !String.IsNullOrWhiteSpace(preferred) ? preferred :
                (!String.IsNullOrWhiteSpace(OtherTitle) ? OtherTitle : (Id ?? "Xmana"));
        }

        public string GetDescription(bool arabic)
        {
            var preferred = arabic ? DescriptionAr : DescriptionEn;
            return !String.IsNullOrWhiteSpace(preferred) ? preferred :
                (DescriptionEn ?? DescriptionAr ?? String.Empty);
        }

        public static VideoItem FromToken(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            var item = new VideoItem
            {
                Id = Read(token, "nb", "id", "videoId"),
                EnglishTitle = Read(token, "en_title"),
                ArabicTitle = Read(token, "ar_title"),
                OtherTitle = Read(token, "other_title"),
                Year = Read(token, "year", "mDate"),
                Kind = Read(token, "kind"),
                Season = Read(token, "season"),
                EpisodeNumber = Read(token, "episodeNummer"),
                DescriptionEn = Read(token, "en_content"),
                DescriptionAr = Read(token, "ar_content"),
                Poster = Read(token, "imgMediumThumbObjUrl", "imgObjUrl", "imgMediumThumb", "imgThumbObjUrl", "imgThumb", "img"),
                Backdrop = Read(token, "imgObjUrl", "img"),
                Rating = Read(token, "filmRating", "seriesRating", "stars", "rate"),
                Duration = Read(token, "duration"),
                RootSeries = Read(token, "rootSeries"),
                ImdbReference = Read(token, "imdbUrlRef"),
                Trailer = Read(token, "trailer"),
                VideoViews = Read(token, "videoViewsNumber"),
                VideoLikes = Read(token, "videoLikesNumber"),
                VideoDislikes = Read(token, "videoDisLikesNumber"),
                ParentSkipping = Read(token, "parent_skipping"),
                ArabicSubtitleFile = Read(token, "arTranslationFile"),
                EnglishSubtitleFile = Read(token, "enTranslationFile"),
                SpanishSubtitleFile = Read(token, "spTranslationFile"),
                IsSpecial = ReadBoolean(token, "isSpecial"),
                HasIntroSkipping = ReadBoolean(token, "hasIntroSkipping"),
                ShowComments = ReadBoolean(token, "showComments"),
                IsSeries = false,
                Language = VideoLanguage.FromToken(token["videoLanguages"])
            };

            item.Categories = ReadList(token["categories"], CategoryInfo.FromToken);
            item.Actors = ReadList(token["actorsInfo"], StaffItem.FromToken);
            item.Directors = ReadList(token["directorsInfo"], StaffItem.FromToken);
            item.Writers = ReadList(token["writersInfo"], StaffItem.FromToken);
            return item;
        }

        private static List<T> ReadList<T>(JToken token, Func<JToken, T> convert)
        {
            var result = new List<T>();
            var array = token as JArray;
            if (array == null) return result;
            foreach (var entry in array)
            {
                var value = convert(entry);
                if (value != null) result.Add(value);
            }
            return result;
        }

        private static string Read(JToken token, params string[] names)
        {
            if (token == null) return String.Empty;
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

        private static bool ReadBoolean(JToken token, string name)
        {
            var value = token?[name];
            if (value == null || value.Type == JTokenType.Null) return false;
            bool parsed;
            if (Boolean.TryParse(value.ToString(), out parsed)) return parsed;
            int number;
            return Int32.TryParse(value.ToString(), out number) && number != 0;
        }
    }

    public sealed class VideoLanguage
    {
        public string EnglishTitle { get; set; }
        public string ArabicTitle { get; set; }

        public static VideoLanguage FromToken(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            return new VideoLanguage
            {
                EnglishTitle = token["en_title"]?.ToString(),
                ArabicTitle = token["ar_title"]?.ToString()
            };
        }
    }

    public sealed class CategoryInfo
    {
        public string EnglishTitle { get; set; }
        public string ArabicTitle { get; set; }

        public static CategoryInfo FromToken(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            return new CategoryInfo
            {
                EnglishTitle = token["en_title"]?.ToString(),
                ArabicTitle = token["ar_title"]?.ToString()
            };
        }

        public string GetTitle(bool arabic)
        {
            return arabic ? (ArabicTitle ?? EnglishTitle) : (EnglishTitle ?? ArabicTitle);
        }
    }

    public sealed class StaffItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Role { get; set; }
        public string Image { get; set; }

        public static StaffItem FromToken(JToken token)
        {
            if (token == null || token.Type != JTokenType.Object) return null;
            return new StaffItem
            {
                Id = token["nb"]?.ToString(),
                Name = token["name"]?.ToString(),
                Role = token["role"]?.ToString(),
                Image = token["staff_img_medium_thumb"]?.ToString() ??
                    token["staff_img_thumb"]?.ToString() ?? token["staff_img"]?.ToString()
            };
        }
    }
}
