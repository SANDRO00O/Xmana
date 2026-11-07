using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Details
{
    /// <summary>The slice of the API the details and staff screens need.</summary>
    public interface IDetailsDataSource
    {
        Task<ApiResult<VideoDetails?>> GetVideoInfoAsync(string id, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<SeasonInfo>>> GetSeasonsAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetEpisodesAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetRelatedAsync(string id, string kind, RequestPriority priority, CancellationToken cancellationToken);

        Task<RecommendationResult> GetRecommendationsAsync(string videoId, string title, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<StaffPage>> GetStaffAsync(string staffId, string lang, RequestPriority priority, CancellationToken cancellationToken);
    }

    public sealed class ApiDetailsDataSource : IDetailsDataSource
    {
        private readonly ICinemanaApi _api;

        public ApiDetailsDataSource(ICinemanaApi api)
        {
            _api = api ?? throw new System.ArgumentNullException(nameof(api));
        }

        public Task<ApiResult<VideoDetails?>> GetVideoInfoAsync(string id, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetVideoInfoAsync(id, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<SeasonInfo>>> GetSeasonsAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetSeasonsAsync(seriesId, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetEpisodesAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetEpisodesAsync(seriesId, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetRelatedAsync(string id, string kind, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetRelatedVideosAsync(id, kind, priority, cancellationToken);

        public Task<RecommendationResult> GetRecommendationsAsync(string videoId, string title, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetRecommendationsAsync(videoId, title, priority, cancellationToken);

        public Task<ApiResult<StaffPage>> GetStaffAsync(string staffId, string lang, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetStaffAsync(staffId, lang, priority, cancellationToken);
    }
}
