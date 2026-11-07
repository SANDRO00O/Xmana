using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Api
{
    /// <summary>
    /// Strongly typed, cancellable access to the documented public API (PROMPT.md section 64). Every call returns
    /// an <see cref="ApiResult{T}"/> so the UI knows whether data is fresh, cached or being refreshed. Failures throw
    /// <see cref="ApiException"/>; cancellation throws OperationCanceledException. No method touches authentication.
    /// </summary>
    public interface ICinemanaApi
    {
        /// <summary>The access level this client sends (API.md <c>level</c>).</summary>
        int Level { get; }

        /// <summary>Raised with the cache key of a response that was refreshed in the background after stale data was served.</summary>
        event System.EventHandler<string> BackgroundRefreshed;

        // video information
        Task<ApiResult<VideoDetails?>> GetVideoInfoAsync(string id, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetRelatedVideosAsync(string id, string kind, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<ParentalStatus>> GetParentalStatusAsync(string id, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoComment>>> GetCommentsAsync(string id, int limit, int offset, CancellationToken cancellationToken = default(CancellationToken));

        // media (never cached: may contain signed URLs)
        Task<ApiResult<IReadOnlyList<VideoQuality>>> GetVideoFilesAsync(string id, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<TranslationInfo>> GetTranslationsAsync(string id, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<SkipInfo>> GetSkipInfoAsync(string id, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<ThumbnailInfo>> GetThumbnailsAsync(string id, CancellationToken cancellationToken = default(CancellationToken));

        // series
        Task<ApiResult<IReadOnlyList<SeasonInfo>>> GetSeasonsAsync(string seriesId, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>Flat episode list as sent by the server; use <c>CatalogMapper.GroupBySeason</c> to arrange it.</summary>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetEpisodesAsync(string seriesId, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        // discovery
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetBannerAsync(RequestPriority priority = RequestPriority.Hero, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestMoviesAsync(int page, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestSeriesAsync(int page, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetFeaturedMoviesAsync(RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        /// <param name="offsetAsCount">Observed to act as a requested result count (API.md section 8).</param>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetNewlyVideosAsync(int offsetAsCount, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        /// <param name="offsetAsCount">Observed to act as a requested result count (API.md section 8).</param>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestVideosAsync(int offsetAsCount, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>May legitimately return an empty list (documented as empty during testing).</summary>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetPopularAsync(PopularPeriod period, int page, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        // groups and collections
        Task<ApiResult<IReadOnlyList<VideoGroup>>> GetGroupsAsync(string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetGroupPageAsync(string groupId, int page, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<JToken>>> GetCollectionIdsAsync(RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetCollectionVideosAsync(string collectionId, RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken));

        // categories and listings
        Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetMainCategoriesAsync(string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetSubCategoriesAsync(string lang, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>> GetCategoryLanguageGroupsAsync(RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        /// <param name="offset">Backend-defined position/range (not a page number).</param>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideosByCategoryAsync(string categoryId, ListingKind kind, string orderBy, int offset, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideosByCategoryAndLanguageAsync(string categoryId, string languageId, ListingKind kind, string orderBy, int offset, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        /// <param name="pageNumber">Zero-based page (this endpoint uses pages, not offsets).</param>
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetVideoListingAsync(ListingKind kind, string sortParam, int itemsPerPage, int pageNumber, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        // search
        Task<ApiResult<IReadOnlyList<VideoSummary>>> SearchAsync(string keyword, int pageSize, int pageNumber, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<VideoSummary>>> AdvancedSearchAsync(AdvancedSearchFilters filters, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        // staff, faq
        Task<ApiResult<StaffPage>> GetStaffAsync(string staffId, string lang, RequestPriority priority = RequestPriority.Primary, CancellationToken cancellationToken = default(CancellationToken));

        Task<ApiResult<IReadOnlyList<FaqEntry>>> GetFaqAsync(RequestPriority priority = RequestPriority.Low, CancellationToken cancellationToken = default(CancellationToken));

        // external public services
        Task<RatingSummary> GetRatingSummaryAsync(IReadOnlyList<RatingTarget> targets, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Public recommendation service (API.md section 14): sends the verified body
        /// <c>{ MovieId, MovieName, ReProcessIfExpired: true }</c>. Never cached and never logged (items may hold temporary URLs).
        /// The service is optional: callers fall back to <see cref="GetRelatedVideosAsync"/> or hide the section on failure.
        /// </summary>
        Task<RecommendationResult> GetRecommendationsAsync(string videoId, string videoTitle, RequestPriority priority = RequestPriority.Secondary, CancellationToken cancellationToken = default(CancellationToken));
    }
}
