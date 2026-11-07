using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Api
{
    public sealed class CinemanaApiOptions
    {
        /// <summary>API.md "level": access/content level used by the client. Tested at 0; no other value is assumed.</summary>
        public int Level { get; set; }
    }

    /// <summary>Default <see cref="ICinemanaApi"/>: endpoint builders + HTTP client + cache source + tolerant mappers.</summary>
    public sealed class CinemanaApi : ICinemanaApi
    {
        private readonly ApiHttpClient _http;
        private readonly CachedJsonSource _cache;
        private readonly int _level;

        public CinemanaApi(ApiHttpClient http, CachedJsonSource cache, CinemanaApiOptions? options = null)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _level = (options ?? new CinemanaApiOptions()).Level;
            _cache.BackgroundRefreshed += (sender, key) => BackgroundRefreshed?.Invoke(this, key);
        }

        public int Level => _level;

        public event EventHandler<string>? BackgroundRefreshed;

        // ---- video information -----------------------------------------------------------------

        public Task<ApiResult<VideoDetails?>> GetVideoInfoAsync(string id, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("allVideoInfo", CinemanaEndpoints.AllVideoInfo(id), CacheKind.VideoMetadata, priority, VideoMapper.ToDetails, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetRelatedVideosAsync(string id, string kind, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("relatedVideos", CinemanaEndpoints.RelatedVideos(id, kind, _level), CacheKind.Related, priority, cancellationToken);

        public Task<ApiResult<ParentalStatus>> GetParentalStatusAsync(string id, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("checkVideoParentalLevel", CinemanaEndpoints.ParentalLevel(id), CacheKind.None, RequestPriority.Hero, PlaybackMapper.ToParentalStatus, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoComment>>> GetCommentsAsync(string id, int limit, int offset, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("videoComment", CinemanaEndpoints.VideoComments(id, limit, offset), CacheKind.None, RequestPriority.Low, PlaybackMapper.ToComments, cancellationToken);

        // ---- media -----------------------------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<VideoQuality>>> GetVideoFilesAsync(string id, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("transcoddedFiles", CinemanaEndpoints.TranscodedFiles(id), CacheKind.None, RequestPriority.Hero, PlaybackMapper.ToQualities, cancellationToken);

        public Task<ApiResult<TranslationInfo>> GetTranslationsAsync(string id, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("translationFiles", CinemanaEndpoints.TranslationFiles(id), CacheKind.None, RequestPriority.Hero, PlaybackMapper.ToTranslations, cancellationToken);

        public Task<ApiResult<SkipInfo>> GetSkipInfoAsync(string id, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("skippingDurations", CinemanaEndpoints.SkippingDurations(id), CacheKind.None, RequestPriority.Primary, PlaybackMapper.ToSkipInfo, cancellationToken);

        public Task<ApiResult<ThumbnailInfo>> GetThumbnailsAsync(string id, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("video_thumbnails", CinemanaEndpoints.VideoThumbnails(id), CacheKind.None, RequestPriority.Low, PlaybackMapper.ToThumbnails, cancellationToken);

        // ---- series ----------------------------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<SeasonInfo>>> GetSeasonsAsync(string seriesId, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("videoSeasonNumber", CinemanaEndpoints.SeasonNumbers(seriesId), CacheKind.Seasons, priority, CatalogMapper.ToSeasons, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetEpisodesAsync(string seriesId, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("videoSeason", CinemanaEndpoints.Episodes(seriesId), CacheKind.Episodes, priority, cancellationToken);

        // ---- discovery -------------------------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetBannerAsync(RequestPriority priority = RequestPriority.Hero, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("banner", CinemanaEndpoints.Banner(_level), CacheKind.Banner, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestMoviesAsync(int page, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("latestMovies", CinemanaEndpoints.LatestMovies(_level, page), CacheKind.Latest, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestSeriesAsync(int page, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("latestSeries", CinemanaEndpoints.LatestSeries(_level, page), CacheKind.Latest, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetFeaturedMoviesAsync(RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("featuredMovies", CinemanaEndpoints.FeaturedMovies(_level), CacheKind.Featured, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetNewlyVideosAsync(int offsetAsCount, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("newlyVideosItems", CinemanaEndpoints.NewlyVideos(_level, offsetAsCount), CacheKind.Latest, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestVideosAsync(int offsetAsCount, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("latestVideosItems", CinemanaEndpoints.LatestVideos(_level, offsetAsCount), CacheKind.Latest, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetPopularAsync(PopularPeriod period, int page, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("popularFilms", CinemanaEndpoints.PopularFilms(period, _level, page), CacheKind.Popular, priority, cancellationToken);

        // ---- groups and collections ------------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<VideoGroup>>> GetGroupsAsync(string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("videoGroups", CinemanaEndpoints.VideoGroups(lang, _level), CacheKind.Groups, priority, CatalogMapper.ToGroups, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetGroupPageAsync(string groupId, int page, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("videoListPagination", CinemanaEndpoints.GroupPage(groupId, _level, page), CacheKind.Listing, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<JToken>>> GetCollectionIdsAsync(RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("collectionsId", CinemanaEndpoints.CollectionIds(), CacheKind.Listing, priority, JsonItems.Extract, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetCollectionVideosAsync(string collectionId, RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("collectionVideos", CinemanaEndpoints.CollectionVideos(collectionId, _level), CacheKind.Listing, priority, cancellationToken);

        // ---- categories and listings -----------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetMainCategoriesAsync(string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("mainCategories", CinemanaEndpoints.MainCategories(lang), CacheKind.Categories, priority, t => CatalogMapper.ToCategories(t, true), cancellationToken);

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetSubCategoriesAsync(string lang, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("subCategories", CinemanaEndpoints.SubCategories(lang), CacheKind.Categories, priority, t => CatalogMapper.ToCategories(t, false), cancellationToken);

        public Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>> GetCategoryLanguageGroupsAsync(RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("categoryLangFreqVideo", CinemanaEndpoints.CategoryLanguageGroups(), CacheKind.Categories, priority, CatalogMapper.ToCategoryLanguageGroups, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideosByCategoryAsync(string categoryId, ListingKind kind, string orderBy, int offset, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("videosByCategory", CinemanaEndpoints.VideosByCategory(categoryId, orderBy, kind, offset, _level), CacheKind.Listing, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideosByCategoryAndLanguageAsync(string categoryId, string languageId, ListingKind kind, string orderBy, int offset, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("videosByCategoryAndLanguage", CinemanaEndpoints.VideosByCategoryAndLanguage(categoryId, languageId, kind, orderBy, _level, offset), CacheKind.Listing, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideoListingAsync(ListingKind kind, string sortParam, int itemsPerPage, int pageNumber, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("videoListing", CinemanaEndpoints.VideoListing(itemsPerPage, _level, kind, sortParam, pageNumber), CacheKind.Listing, priority, cancellationToken);

        // ---- search ----------------------------------------------------------------------------

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> SearchAsync(string keyword, int pageSize, int pageNumber, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                // Nothing to look for: no request is sent.
                return Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(new VideoSummary[0], DataOrigin.Network, null));
            }

            return GetVideosAsync("videoSearch", CinemanaEndpoints.SimpleSearch(keyword.Trim(), pageSize, pageNumber, _level), CacheKind.Search, priority, cancellationToken);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> AdvancedSearchAsync(AdvancedSearchFilters filters, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetVideosAsync("AdvancedSearch", CinemanaEndpoints.AdvancedSearch(_level, filters), CacheKind.Search, priority, cancellationToken);

        // ---- staff, faq ------------------------------------------------------------------------

        public Task<ApiResult<StaffPage>> GetStaffAsync(string staffId, string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("staff", CinemanaEndpoints.Staff(staffId, lang, _level), CacheKind.Staff, priority, CatalogMapper.ToStaffPage, cancellationToken);

        public Task<ApiResult<IReadOnlyList<FaqEntry>>> GetFaqAsync(RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken)) =>
            GetAsync("faqQuestion", CinemanaEndpoints.Faq(), CacheKind.Faq, priority, CatalogMapper.ToFaq, cancellationToken);

        // ---- external public services ----------------------------------------------------------

        public async Task<RatingSummary> GetRatingSummaryAsync(IReadOnlyList<RatingTarget> targets, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken))
        {
            var unique = new List<RatingTarget>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var target in targets ?? new RatingTarget[0])
            {
                if (target != null && seen.Add(target.TargetTypeName + ":" + target.TargetId))
                {
                    unique.Add(target);
                }
            }

            if (unique.Count == 0)
            {
                return new RatingSummary(new JToken[0], null, new JObject());
            }

            var body = new JObject
            {
                { "targets", new JArray(unique.Select(t => new JObject { { "targetId", t.TargetId }, { "targetType", t.TargetTypeName } })) },
            };

            var token = await _http.PostJsonAsync("ratingSummary", new Uri(CinemanaEndpoints.RatingSummaryUrl), body.ToString(Newtonsoft.Json.Formatting.None), priority, cancellationToken).ConfigureAwait(false);
            return PlaybackMapper.ToRatingSummary(token);
        }

        public async Task<RecommendationResult> GetRecommendationsAsync(string videoId, string videoTitle, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException("A video id is required.", nameof(videoId));
            }

            // Verified request body (API.md section 14). MovieName is sent exactly as known; it may be empty if the title is unknown.
            var body = new JObject
            {
                { "MovieId", videoId.Trim() },
                { "MovieName", videoTitle?.Trim() ?? string.Empty },
                { "ReProcessIfExpired", true },
            };

            var token = await _http.PostJsonAsync("recommendation", new Uri(CinemanaEndpoints.RecommendationUrl), body.ToString(Newtonsoft.Json.Formatting.None), priority, cancellationToken).ConfigureAwait(false);
            return ExternalServiceMapper.ToRecommendations(token);
        }

        // ---- helpers ---------------------------------------------------------------------------

        private Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideosAsync(string label, string relative, CacheKind kind, RequestPriority priority, CancellationToken cancellationToken) =>
            GetAsync(label, relative, kind, priority, token => VideoMapper.ToSummaries(JsonItems.Extract(token)), cancellationToken);

        private async Task<ApiResult<T>> GetAsync<T>(
            string label,
            string relative,
            CacheKind kind,
            RequestPriority priority,
            Func<JToken, T> map,
            CancellationToken cancellationToken)
        {
            var uri = CinemanaEndpoints.Resolve(relative);
            var cached = await _cache.GetAsync(
                relative,
                kind,
                token => _http.GetJsonAsync(label, uri, priority, token),
                cancellationToken).ConfigureAwait(false);

            return new ApiResult<T>(map(cached.Token), cached.Origin, cached.StoredUtc);
        }
    }
}
