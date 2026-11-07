using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Api.Mapping
{
    /// <summary>
    /// Categories, groups, staff, FAQ, seasons. Where API.md does not document field names
    /// (category id, staff payload layout, FAQ fields) the mapper reads defensively and keeps the raw object;
    /// those spots are listed in docs/api-notes.md and must be confirmed against live data.
    /// </summary>
    public static class CatalogMapper
    {
        public static IReadOnlyList<CategoryInfo> ToCategories(JToken? token, bool isMain)
        {
            var result = new List<CategoryInfo>();
            foreach (var item in JsonItems.Extract(token))
            {
                var obj = item as JObject;
                if (obj == null)
                {
                    continue;
                }

                var en = JsonRead.Text(JsonRead.Get(obj, "en_title"));
                var ar = JsonRead.Text(JsonRead.Get(obj, "ar_title"));
                if (en == null && ar == null)
                {
                    continue;
                }

                // API.md section 9 / 15: category entries expose their id as "nb" (e.g. 84 = Action).
                var id = JsonRead.Text(JsonRead.Get(obj, "nb"));
                result.Add(new CategoryInfo(id, en, ar, isMain, obj));
            }

            return result;
        }

        public static IReadOnlyList<CategoryLanguageGroup> ToCategoryLanguageGroups(JToken? token)
        {
            var result = new List<CategoryLanguageGroup>();
            foreach (var item in JsonItems.Extract(token))
            {
                var obj = item as JObject;
                var id = JsonRead.Text(JsonRead.Get(obj, "nb"));
                if (obj == null || id == null)
                {
                    continue;
                }

                var languages = new List<CategoryLanguage>();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var array = JsonRead.Array(JsonRead.Get(obj, "langArray"));
                if (array != null)
                {
                    foreach (var entry in array)
                    {
                        var lang = entry as JObject;
                        var languageId = JsonRead.Text(JsonRead.Get(lang, "langNb"));
                        if (lang == null || languageId == null || !seen.Add(languageId))
                        {
                            continue;
                        }

                        languages.Add(new CategoryLanguage(
                            languageId,
                            JsonRead.Int(JsonRead.Get(lang, "langFreq")),
                            JsonRead.Int(JsonRead.Get(lang, "langFreqSeries")),
                            JsonRead.Text(JsonRead.Get(lang, "lang_ar_title")),
                            JsonRead.Text(JsonRead.Get(lang, "lang_en_title")),
                            lang));
                    }
                }

                result.Add(new CategoryLanguageGroup(
                    id,
                    JsonRead.Text(JsonRead.Get(obj, "en_title")),
                    JsonRead.Text(JsonRead.Get(obj, "ar_title")),
                    languages,
                    obj));
            }

            return result;
        }

        public static IReadOnlyList<VideoGroup> ToGroups(JToken? token)
        {
            // Documented wrapper: { "groups": [ ... ] }. A bare list is accepted as well.
            var list = JsonRead.Array(JsonRead.Get(token, "groups")) ?? token as JArray;
            var result = new List<VideoGroup>();
            if (list == null)
            {
                return result;
            }

            foreach (var item in list)
            {
                var obj = item as JObject;
                var id = JsonRead.Text(JsonRead.Get(obj, "groupsID"));
                if (obj == null || id == null)
                {
                    continue;
                }

                var content = JsonRead.Get(obj, "content");
                result.Add(new VideoGroup(
                    id,
                    JsonRead.Text(JsonRead.Get(obj, "title")),
                    JsonRead.Bool(JsonRead.Get(obj, "hasPagination")),
                    VideoMapper.ToSummaries(JsonItems.Extract(content)),
                    obj));
            }

            return result;
        }

        public static StaffPage ToStaffPage(JToken token)
        {
            // Layout is only loosely documented ("a staff object and 24 associated items"). Structural detection:
            // the staff object is the object (root or one level down) carrying staff fields; titles are the video list.
            StaffMember? staff = null;
            var root = token as JObject;
            if (root != null)
            {
                staff = LooksLikeStaff(root) ? VideoMapper.ToStaff(root) : null;
                if (staff == null)
                {
                    foreach (var property in root.Properties())
                    {
                        var nested = property.Value as JObject;
                        if (nested != null && LooksLikeStaff(nested))
                        {
                            staff = VideoMapper.ToStaff(nested);
                            if (staff != null)
                            {
                                break;
                            }
                        }
                    }
                }
            }

            return new StaffPage(staff, VideoMapper.ToSummaries(JsonItems.Extract(token)), token);
        }

        public static IReadOnlyList<FaqEntry> ToFaq(JToken? token)
        {
            return JsonItems.Extract(token).OfType<JObject>().Select(o => new FaqEntry(o)).ToList();
        }

        public static IReadOnlyList<SeasonInfo> ToSeasons(JToken? token)
        {
            var result = new List<SeasonInfo>();
            var seen = new HashSet<int>();
            foreach (var item in JsonItems.Extract(token))
            {
                // UNVERIFIED field name: the items "exposed the season number field"; episodes document it as "season".
                var number = JsonRead.Int(JsonRead.Get(item, "season")) ?? JsonRead.Int(item);
                if (number.HasValue && seen.Add(number.Value))
                {
                    result.Add(new SeasonInfo(number.Value, item));
                }
            }

            return result.OrderBy(s => s.Number).ToList();
        }

        /// <summary>
        /// Groups the flat <c>videoSeason</c> episode list by the server's <c>season</c> and orders by
        /// <c>episodeNummer</c> (API.md: array position is not reliable). Episodes lacking a number keep their relative
        /// order after numbered ones; episodes lacking a season go in a final null-season group.
        /// </summary>
        public static IReadOnlyList<SeasonGroup> GroupBySeason(IReadOnlyList<VideoSummary> episodes)
        {
            var indexed = episodes.Select((episode, index) => new { episode, index }).ToList();
            var groups = indexed
                .GroupBy(x => x.episode.Season)
                .OrderBy(g => g.Key.HasValue ? 0 : 1)
                .ThenBy(g => g.Key ?? 0)
                .Select(g => new SeasonGroup(
                    g.Key,
                    g.OrderBy(x => x.episode.EpisodeNumber.HasValue ? 0 : 1)
                        .ThenBy(x => x.episode.EpisodeNumber ?? 0)
                        .ThenBy(x => x.index)
                        .Select(x => x.episode)
                        .ToList()))
                .ToList();
            return groups;
        }

        private static bool LooksLikeStaff(JObject obj) =>
            JsonRead.Get(obj, "name") != null && (JsonRead.Get(obj, "staff_img") != null || JsonRead.Get(obj, "role") != null || JsonRead.Get(obj, "nb") != null);
    }
}
