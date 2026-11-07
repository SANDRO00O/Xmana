using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Api.Mapping
{
    public static class PlaybackMapper
    {
        private static readonly Regex QualityLabel = new Regex("^\\s*(\\d{3,4})\\s*p\\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Detects qualities dynamically from <c>transcoddedFiles</c>. The response layout is not documented
        /// (signed URLs are omitted from API.md), so detection is by VALUE, not field name: a quality is a string
        /// matching "&lt;height&gt;p" paired with an HTTPS URL found in the same object, or an object keyed by such
        /// a label whose value is (or contains) a URL. Nothing is assumed to exist; result is sorted by height.
        /// </summary>
        public static IReadOnlyList<VideoQuality> ToQualities(JToken? token)
        {
            var found = new Dictionary<int, VideoQuality>();

            foreach (var obj in EnumerateObjects(token))
            {
                // Case 1: { "<label>": url } style
                foreach (var property in obj.Properties())
                {
                    var match = QualityLabel.Match(property.Name);
                    if (match.Success)
                    {
                        var url = FindHttpsUrl(property.Value);
                        if (url != null)
                        {
                            Add(found, match.Groups[1].Value, url);
                        }
                    }
                }

                // Case 2: one object holds a label value and a URL value
                string? label = null;
                string? link = null;
                foreach (var property in obj.Properties())
                {
                    var text = JsonRead.String(property.Value);
                    if (text == null)
                    {
                        continue;
                    }

                    var match = QualityLabel.Match(text);
                    if (label == null && match.Success)
                    {
                        label = match.Groups[1].Value;
                    }
                    else if (link == null && UrlValidator.TryGetHttpsUri(text, out var _))
                    {
                        link = text.Trim();
                    }
                }

                if (label != null && link != null)
                {
                    Add(found, label, link);
                }
            }

            return found.Values.OrderBy(q => q.Height).ToList();
        }

        public static TranslationInfo ToTranslations(JToken token)
        {
            var obj = token as JObject ?? new JObject();
            var tracks = new List<SubtitleTrack>();
            AddTrack(tracks, SubtitleLanguage.Arabic, JsonRead.Text(JsonRead.Get(obj, "arTranslationFile")));
            AddTrack(tracks, SubtitleLanguage.English, JsonRead.Text(JsonRead.Get(obj, "enTranslationFile")));
            AddTrack(tracks, SubtitleLanguage.Spanish, JsonRead.Text(JsonRead.Get(obj, "spTranslationFile")));
            return new TranslationInfo(
                tracks,
                JsonRead.Text(JsonRead.Get(obj, "arTranslationFilePath")),
                JsonRead.Text(JsonRead.Get(obj, "enTranslationFilePath")),
                obj);
        }

        /// <summary>
        /// Builds skip ranges from <c>start[]</c>/<c>end[]</c>. Only index-matched pairs with start &lt; end are kept; the
        /// pairing rule is an assumption (API.md only shows empty arrays) and the raw payload stays available.
        /// Nothing is invented: unequal or non-numeric data yields fewer (or zero) ranges.
        /// </summary>
        public static SkipInfo ToSkipInfo(JToken token)
        {
            var starts = Numbers(JsonRead.Get(token, "start"));
            var ends = Numbers(JsonRead.Get(token, "end"));
            var ranges = new List<SkipRange>();
            var pairs = Math.Min(starts.Count, ends.Count);
            for (var i = 0; i < pairs; i++)
            {
                if (starts[i].HasValue && ends[i].HasValue && starts[i]!.Value >= 0 && starts[i]!.Value < ends[i]!.Value)
                {
                    ranges.Add(new SkipRange(starts[i]!.Value, ends[i]!.Value));
                }
            }

            return new SkipInfo(ranges, starts.Count, ends.Count, token);
        }

        public static ThumbnailInfo ToThumbnails(JToken token)
        {
            var list = JsonRead.Array(JsonRead.Get(token, "videoThumbnails"));
            var urls = new List<string>();
            if (list != null)
            {
                foreach (var item in list)
                {
                    var url = FindHttpsUrl(item);
                    if (url != null)
                    {
                        urls.Add(url);
                    }
                }
            }

            return new ThumbnailInfo(JsonRead.Int(JsonRead.Get(token, "status")), urls, list?.Count ?? 0, token);
        }

        public static ParentalStatus ToParentalStatus(JToken token) =>
            new ParentalStatus(JsonRead.Text(JsonRead.Get(token, "status")), token);

        public static IReadOnlyList<VideoComment> ToComments(JToken? token) =>
            JsonItems.Extract(token).OfType<JObject>().Select(o => new VideoComment(o)).ToList();

        public static RatingSummary ToRatingSummary(JToken token)
        {
            var items = JsonRead.Array(JsonRead.Get(token, "items"));
            return new RatingSummary(
                items != null ? (IReadOnlyList<JToken>)items.ToList() : new JToken[0],
                JsonRead.Int(JsonRead.Get(token, "totalCount")),
                token);
        }

        private static void AddTrack(List<SubtitleTrack> tracks, SubtitleLanguage language, string? file)
        {
            if (!string.IsNullOrEmpty(file))
            {
                tracks.Add(new SubtitleTrack(language, file!));
            }
        }

        private static List<double?> Numbers(JToken? token)
        {
            var array = JsonRead.Array(token);
            return array == null ? new List<double?>() : array.Select(item => JsonRead.Double(item)).ToList();
        }

        private static void Add(Dictionary<int, VideoQuality> found, string heightText, string url)
        {
            int height;
            if (int.TryParse(heightText, out height) && !found.ContainsKey(height))
            {
                found[height] = new VideoQuality(height + "p", height, url);
            }
        }

        private static IEnumerable<JObject> EnumerateObjects(JToken? token)
        {
            if (token == null)
            {
                yield break;
            }

            var obj = token as JObject;
            if (obj != null)
            {
                yield return obj;
                foreach (var child in obj.Properties().Select(p => p.Value))
                {
                    foreach (var nested in EnumerateObjects(child))
                    {
                        yield return nested;
                    }
                }

                yield break;
            }

            var array = token as JArray;
            if (array != null)
            {
                foreach (var child in array)
                {
                    foreach (var nested in EnumerateObjects(child))
                    {
                        yield return nested;
                    }
                }
            }
        }

        private static string? FindHttpsUrl(JToken? token)
        {
            var text = JsonRead.String(token);
            if (text != null)
            {
                return UrlValidator.TryGetHttpsUri(text, out var _) ? text.Trim() : null;
            }

            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var property in obj.Properties())
                {
                    var inner = JsonRead.String(property.Value);
                    if (inner != null && UrlValidator.TryGetHttpsUri(inner, out var _))
                    {
                        return inner.Trim();
                    }
                }
            }

            return null;
        }
    }
}
