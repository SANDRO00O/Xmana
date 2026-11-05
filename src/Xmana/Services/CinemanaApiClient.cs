using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Xmana.Services
{
    /// <summary>Public/read-oriented Cinemana API client. Paths and request fields mirror API.md.</summary>
    public sealed class CinemanaApiClient
    {
        public const string BaseUrl = "https://cinemana.shabakaty.cc/api/android/";
        private const string RecommendationUrl = "https://recommend.shabakaty.cc/api/recommendation/recommend";
        private const string RatingsUrl = "https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types";
        private const string SeasonPostersUrl = "https://thumbnail.shabakaty.cc/api/series/seasonposters";
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(22) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Xmana/0.1 (Xbox UWP)");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            return client;
        }

        private static string Segment(string value) => Uri.EscapeDataString(value ?? String.Empty);

        private async Task<JToken> GetAsync(string path)
        {
            using (var response = await Client.GetAsync(new Uri(new Uri(BaseUrl), path)))
            {
                var body = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return String.IsNullOrWhiteSpace(body) ? JValue.CreateNull() : JToken.Parse(body);
            }
        }

        private async Task<JToken> PostJsonAsync(string absoluteUrl, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
            using (var response = await Client.PostAsync(absoluteUrl, content))
            {
                var body = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return String.IsNullOrWhiteSpace(body) ? JValue.CreateNull() : JToken.Parse(body);
            }
        }

        private async Task<JToken> PostFormAsync(string path, IDictionary<string, string> values)
        {
            using (var content = new FormUrlEncodedContent(values))
            using (var response = await Client.PostAsync(new Uri(new Uri(BaseUrl), path), content))
            {
                var body = await response.Content.ReadAsStringAsync();
                response.EnsureSuccessStatusCode();
                return String.IsNullOrWhiteSpace(body) ? JValue.CreateNull() : JToken.Parse(body);
            }
        }

        public Task<JToken> GetAllVideoInfoAsync(string id) => GetAsync("allVideoInfo/id/" + Segment(id));
        public Task<JToken> GetRelatedVideosAsync(string id, string kind, string level) =>
            GetAsync("relatedVideos/id/" + Segment(id) + "/kind/" + Segment(kind) + "/level/" + Segment(level));
        public Task<JToken> CheckVideoParentalLevelAsync(string id) =>
            GetAsync("checkVideoParentalLevel/id/" + Segment(id));
        public Task<JToken> GetTranscodedFilesAsync(string id) =>
            GetAsync("transcoddedFiles/id/" + Segment(id));
        public Task<JToken> GetTranslationFilesAsync(string id) =>
            GetAsync("translationFiles/id/" + Segment(id));
        public Task<JToken> GetSkippingDurationsAsync(string id) =>
            GetAsync("skippingDurations/id/" + Segment(id));
        public Task<JToken> GetVideoThumbnailsAsync(string id) =>
            GetAsync("video_thumbnails/video_id/" + Segment(id));
        public Task<JToken> GetVideoCommentsAsync(string id, int limit, int offset) =>
            GetAsync("videoComment/id/" + Segment(id) + "/limit/" + limit + "/offset/" + offset);
        public Task<JToken> GetVideoSeasonNumberAsync(string id) =>
            GetAsync("videoSeasonNumber/id/" + Segment(id));
        public Task<JToken> GetVideoSeasonAsync(string id) =>
            GetAsync("videoSeason/id/" + Segment(id));

        public Task<JToken> GetBannersAsync(string level) => GetAsync("banner/level/" + Segment(level));
        public Task<JToken> GetVideoGroupsAsync(string lang, string level) =>
            GetAsync("videoGroups/lang/" + Segment(lang) + "/level/" + Segment(level));
        public Task<JToken> GetNewlyVideosItemsAsync(string level, int count) =>
            GetAsync("newlyVideosItems/level/" + Segment(level) + "/offset/" + count + "/");
        public Task<JToken> GetLatestVideosItemsAsync(string level, int count) =>
            GetAsync("latestVideosItems/level/" + Segment(level) + "/offset/" + count + "/");
        public Task<JToken> GetLatestMoviesAsync(string level, int page) =>
            GetAsync("latestMovies/level/" + Segment(level) + "/itemsPerPage/24/page/" + page + "/");
        public Task<JToken> GetLatestSeriesAsync(string level, int page) =>
            GetAsync("latestSeries/level/" + Segment(level) + "/itemsPerPage/24/page/" + page + "/");
        public Task<JToken> GetFeaturedMoviesAsync(string level) =>
            GetAsync("featuredMovies/level/" + Segment(level) + "/itemsPerPage/12/page/0");
        public Task<JToken> GetPopularFilmsAsync(string period, string level, int page) =>
            GetAsync("popularFilms/kind/" + Segment(period) + "/level/" + Segment(level) + "/page/" + page);

        public Task<JToken> GetMainCategoriesAsync(string lang) =>
            GetAsync("mainCategories?lang=" + Segment(lang));
        public Task<JToken> GetSubCategoriesAsync(string lang) =>
            GetAsync("subCategories?lang=" + Segment(lang));
        public Task<JToken> GetCategoryLanguageFrequencyAsync() => GetAsync("categoryLangFreqVideo");
        public Task<JToken> GetVideosByCategoryAsync(string categoryId, string orderBy, string videoKind, int offset, string level) =>
            GetAsync("videosByCategory?categoryID=" + Segment(categoryId) + "&orderby=" + Segment(orderBy) +
                "&videoKind=" + Segment(videoKind) + "&offset=" + offset + "&level=" + Segment(level));
        public Task<JToken> GetVideosByCategoryAndLanguageAsync(string categoryId, string languageId, string videoKind, string orderBy, string level, int offset) =>
            GetAsync("videosByCategoryAndLanguage?category_id=" + Segment(categoryId) + "&language_id=" +
                Segment(languageId) + "&videoKind=" + Segment(videoKind) + "&orderby=" + Segment(orderBy) +
                "&level=" + Segment(level) + "&offset=" + offset);
        public Task<JToken> GetVideoListingAsync(int itemsPerPage, string level, string videoKind, string sortParam, int page) =>
            GetAsync("video/V/2/itemsPerPage/" + itemsPerPage + "/level/" + Segment(level) + "/videoKind/" +
                Segment(videoKind) + "/sortParam/" + Segment(sortParam) + "/pageNumber/" + page);

        public Task<JToken> SearchTitleAsync(string keyword, int page, string level, int itemsPerPage = 12)
        {
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(keyword ?? String.Empty))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return GetAsync("video/V/2/itemsPerPage/20/video_title_search/" + encoded +
                "/itemsPerPage/" + itemsPerPage + "/pageNumber/" + page + "/level/" + Segment(level));
        }

        public Task<JToken> AdvancedSearchAsync(IDictionary<string, string> filters, string level, int page)
        {
            var query = new List<string> { "level=" + Segment(level) };
            if (filters != null)
                foreach (var pair in filters.Where(p => !String.IsNullOrWhiteSpace(p.Value)))
                    query.Add(Segment(pair.Key) + "=" + Segment(pair.Value));
            if (filters == null || !filters.ContainsKey("page")) query.Add("page=" + page);
            return GetAsync("AdvancedSearch?" + String.Join("&", query));
        }

        public Task<JToken> GetGroupPageAsync(string groupId, string level, int page) =>
            GetAsync("videoListPagination/groupID/" + Segment(groupId) + "/level/" + Segment(level) +
                "/itemsPerPage/12/page/" + page);
        public Task<JToken> GetCollectionsAsync() => GetAsync("collectionsId/");
        public Task<JToken> GetCollectionVideosAsync(string collectionId, string level) =>
            GetAsync("collectionVideos/collectionID/" + Segment(collectionId) + "/level/" + Segment(level));
        public Task<JToken> GetStaffAsync(string staffId, string lang, string level) =>
            GetAsync("staff/actorID/" + Segment(staffId) + "/lang/" + Segment(lang) + "/level/" + Segment(level));
        public Task<JToken> GetFaqAsync() => GetAsync("faqQuestion");

        // Discovered in frontend extraction; response schema was not live-verified.
        public Task<JToken> GetVideosByIdsAsync(IEnumerable<string> ids) =>
            PostFormAsync("getVideosByIds", new Dictionary<string, string> {
                { "ids", String.Join(",", ids ?? Enumerable.Empty<string>()) }
            });

        public Task<JToken> GetRecommendationsAsync(string videoId, string title) =>
            PostJsonAsync(RecommendationUrl, new {
                MovieId = videoId, MovieName = title, ReProcessIfExpired = true
            });

        public Task<JToken> GetRatingSummaryAsync(string videoId, string kind, string rootSeries)
        {
            var isEpisode = kind == "2";
            var targetId = !String.IsNullOrWhiteSpace(rootSeries) && rootSeries != "0" ? rootSeries : videoId;
            var targetType = isEpisode ? "Episode" : "Movie";
            return PostJsonAsync(RatingsUrl, new {
                targets = new[] { new { targetId = targetId, targetType = targetType } }
            });
        }

        // The discovered service returned 503 during verification; never use it as a required path.
        public Task<JToken> GetSeasonPostersAsync(string rootSeries) =>
            PostJsonAsync(SeasonPostersUrl, new { RootSeries = rootSeries });

        public static IEnumerable<JToken> AsList(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return Enumerable.Empty<JToken>();
            var array = token as JArray;
            if (array != null) return array;
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var key in new[] { "info", "groups", "items", "data", "videos", "results" })
                    if (obj[key] is JArray found) return found;
            }
            return Enumerable.Empty<JToken>();
        }
    }
}
