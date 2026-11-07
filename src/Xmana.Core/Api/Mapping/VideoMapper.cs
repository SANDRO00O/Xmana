using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Api.Mapping
{
    /// <summary>
    /// Maps remote JSON to domain models without ever throwing on bad data (PROMPT.md section 59).
    /// Reading is by field name from the API.md catalogue; the original object is kept in <c>Raw</c>.
    /// An item without an id cannot be used by the app and is dropped.
    /// </summary>
    public static class VideoMapper
    {
        public static VideoSummary? ToSummary(JToken? token)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                return null;
            }

            var id = JsonRead.Text(JsonRead.Get(obj, "nb"));
            if (id == null)
            {
                return null;
            }

            var kindRaw = JsonRead.Text(JsonRead.Get(obj, "kind"));
            return new VideoSummary(
                id,
                JsonRead.Text(JsonRead.Get(obj, "en_title")),
                JsonRead.Text(JsonRead.Get(obj, "ar_title")),
                JsonRead.Double(JsonRead.Get(obj, "stars")),
                JsonRead.Text(JsonRead.Get(obj, "year")),
                kindRaw,
                ParseKind(kindRaw),
                JsonRead.Int(JsonRead.Get(obj, "season")),
                JsonRead.Int(JsonRead.Get(obj, "episodeNummer")),
                JsonRead.Text(JsonRead.Get(obj, "rootSeries")),
                JsonRead.Text(JsonRead.Get(obj, "img")),
                JsonRead.Text(JsonRead.Get(obj, "imgThumb")),
                JsonRead.Text(JsonRead.Get(obj, "imgMediumThumb")),
                JsonRead.Text(JsonRead.Get(obj, "imgObjUrl")),
                JsonRead.Text(JsonRead.Get(obj, "imgThumbObjUrl")),
                JsonRead.Text(JsonRead.Get(obj, "imgMediumThumbObjUrl")),
                JsonRead.Text(JsonRead.Get(obj, "trailer")),
                MapCategories(JsonRead.Get(obj, "categories")),
                obj);
        }

        /// <summary>Maps a list of video objects; invalid entries are skipped.</summary>
        public static IReadOnlyList<VideoSummary> ToSummaries(IEnumerable<JToken> items)
        {
            var result = new List<VideoSummary>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var summary = ToSummary(item);
                if (summary != null && seen.Add(summary.Id))
                {
                    result.Add(summary);
                }
            }

            return result;
        }

        public static VideoDetails? ToDetails(JToken? token)
        {
            // allVideoInfo is documented as the metadata object itself; accept a single-element list/info wrapper defensively.
            var root = token as JObject;
            if (root != null && JsonRead.Get(root, "nb") == null)
            {
                var wrapped = JsonItems.Extract(root).FirstOrDefault();
                if (wrapped != null)
                {
                    root = wrapped as JObject;
                }
            }
            else if (token is JArray)
            {
                root = JsonItems.Extract(token).FirstOrDefault() as JObject;
            }

            var summary = ToSummary(root);
            if (summary == null || root == null)
            {
                return null;
            }

            return new VideoDetails(
                summary,
                JsonRead.Text(JsonRead.Get(root, "other_title")),
                JsonRead.Text(JsonRead.Get(root, "ar_content")),
                JsonRead.Text(JsonRead.Get(root, "en_content")),
                JsonRead.Text(JsonRead.Get(root, "mDate")),
                JsonRead.Text(JsonRead.Get(root, "itemDate")),
                JsonRead.Text(JsonRead.Get(root, "duration")),
                JsonRead.Text(JsonRead.Get(root, "imdbUrlRef")),
                JsonRead.Text(JsonRead.Get(root, "filmRating")),
                JsonRead.Text(JsonRead.Get(root, "seriesRating")),
                JsonRead.Text(JsonRead.Get(root, "rate")),
                JsonRead.Text(JsonRead.Get(root, "ratingImg")),
                JsonRead.Bool(JsonRead.Get(root, "isSpecial")),
                JsonRead.Bool(JsonRead.Get(root, "showComments")),
                JsonRead.Text(JsonRead.Get(root, "episode_flag")),
                JsonRead.Text(JsonRead.Get(root, "audioStreamNumber")),
                JsonRead.Text(JsonRead.Get(root, "parent_skipping")),
                JsonRead.Text(JsonRead.Get(root, "collectionID")),
                JsonRead.Bool(JsonRead.Get(root, "isDeleted")),
                JsonRead.Bool(JsonRead.Get(root, "useParentImg")),
                JsonRead.Bool(JsonRead.Get(root, "hasIntroSkipping")),
                JsonRead.Bool(JsonRead.Get(root, "castable")),
                JsonRead.Long(JsonRead.Get(root, "videoLikesNumber")),
                JsonRead.Long(JsonRead.Get(root, "videoDisLikesNumber")),
                JsonRead.Int(JsonRead.Get(root, "videoCommentsNumber")),
                JsonRead.Long(JsonRead.Get(root, "videoViewsNumber")),
                JsonRead.Get(root, "videoLanguages"),
                MapStaff(JsonRead.Get(root, "directorsInfo")),
                MapStaff(JsonRead.Get(root, "actorsInfo")),
                MapStaff(JsonRead.Get(root, "writersInfo")));
        }

        public static IReadOnlyList<StaffMember> MapStaff(JToken? token)
        {
            var array = JsonRead.Array(token);
            if (array == null)
            {
                return new StaffMember[0];
            }

            var result = new List<StaffMember>();
            foreach (var item in array)
            {
                var member = ToStaff(item);
                if (member != null)
                {
                    result.Add(member);
                }
            }

            return result;
        }

        public static StaffMember? ToStaff(JToken? token)
        {
            var obj = token as JObject;
            if (obj == null)
            {
                return null;
            }

            var name = JsonRead.Text(JsonRead.Get(obj, "name"));
            var id = JsonRead.Text(JsonRead.Get(obj, "nb"));
            if (name == null && id == null)
            {
                return null;
            }

            return new StaffMember(
                id,
                name,
                JsonRead.Text(JsonRead.Get(obj, "role")),
                JsonRead.Text(JsonRead.Get(obj, "staff_img")),
                JsonRead.Text(JsonRead.Get(obj, "staff_img_thumb")),
                JsonRead.Text(JsonRead.Get(obj, "staff_img_medium_thumb")),
                obj);
        }

        public static VideoKind ParseKind(string? raw)
        {
            switch (raw)
            {
                case "1":
                    return VideoKind.Movie;
                case "2":
                    return VideoKind.Series;
                default:
                    return VideoKind.Unknown;
            }
        }

        private static IReadOnlyList<CategoryName> MapCategories(JToken? token)
        {
            var array = JsonRead.Array(token);
            if (array == null)
            {
                return new CategoryName[0];
            }

            var result = new List<CategoryName>();
            foreach (var item in array)
            {
                var en = JsonRead.Text(JsonRead.Get(item, "en_title"));
                var ar = JsonRead.Text(JsonRead.Get(item, "ar_title"));
                if (en != null || ar != null)
                {
                    result.Add(new CategoryName(en, ar));
                }
            }

            return result;
        }
    }
}
