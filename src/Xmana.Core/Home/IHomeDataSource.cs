using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Home
{
    /// <summary>The slice of the API the Home page needs. Kept narrow so Home logic is testable without the whole API.</summary>
    public interface IHomeDataSource
    {
        event EventHandler<string> BackgroundRefreshed;

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetBannerAsync(RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetFeaturedMoviesAsync(RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestMoviesAsync(int page, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestSeriesAsync(int page, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetNewlyAddedAsync(int count, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetPopularAsync(int page, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoGroup>>> GetGroupsAsync(string lang, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetGroupPageAsync(string groupId, int page, RequestPriority priority, CancellationToken cancellationToken);
    }

    /// <summary>Adapts <see cref="ICinemanaApi"/> to <see cref="IHomeDataSource"/>.</summary>
    public sealed class ApiHomeDataSource : IHomeDataSource
    {
        /// <summary>API.md section 8: the frontend normally asks <c>newlyVideosItems</c> for 12 (the offset acts as a count).</summary>
        public const int NewlyAddedCount = 12;

        private readonly ICinemanaApi _api;

        public ApiHomeDataSource(ICinemanaApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _api.BackgroundRefreshed += (sender, key) => BackgroundRefreshed?.Invoke(this, key);
        }

        public event EventHandler<string>? BackgroundRefreshed;

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetBannerAsync(RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetBannerAsync(priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetFeaturedMoviesAsync(RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetFeaturedMoviesAsync(priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestMoviesAsync(int page, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetLatestMoviesAsync(page, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestSeriesAsync(int page, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetLatestSeriesAsync(page, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetNewlyAddedAsync(int count, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetNewlyVideosAsync(count, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetPopularAsync(int page, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetPopularAsync(PopularPeriod.Month, page, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoGroup>>> GetGroupsAsync(string lang, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetGroupsAsync(lang, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetGroupPageAsync(string groupId, int page, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetGroupPageAsync(groupId, page, priority, cancellationToken);
    }
}
