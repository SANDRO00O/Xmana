using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Discovery
{
    /// <summary>The slice of the API used by browsing, categories and search. Narrow so the logic is testable without the whole API.</summary>
    public interface IDiscoveryDataSource
    {
        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetListingAsync(ListingKind kind, string sort, int itemsPerPage, int pageNumber, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAsync(string categoryId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAndLanguageAsync(string categoryId, string languageId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> SearchAsync(string keyword, int pageSize, int pageNumber, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<VideoSummary>>> AdvancedSearchAsync(AdvancedSearchFilters filters, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetMainCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetSubCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken);

        Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>> GetCategoryLanguageGroupsAsync(RequestPriority priority, CancellationToken cancellationToken);
    }

    public sealed class ApiDiscoveryDataSource : IDiscoveryDataSource
    {
        private readonly ICinemanaApi _api;

        public ApiDiscoveryDataSource(ICinemanaApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetListingAsync(ListingKind kind, string sort, int itemsPerPage, int pageNumber, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetVideoListingAsync(kind, sort, itemsPerPage, pageNumber, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAsync(string categoryId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetVideosByCategoryAsync(categoryId, kind, sort, offset, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAndLanguageAsync(string categoryId, string languageId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetVideosByCategoryAndLanguageAsync(categoryId, languageId, kind, sort, offset, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> SearchAsync(string keyword, int pageSize, int pageNumber, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.SearchAsync(keyword, pageSize, pageNumber, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> AdvancedSearchAsync(AdvancedSearchFilters filters, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.AdvancedSearchAsync(filters, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetMainCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetMainCategoriesAsync(lang, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetSubCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetSubCategoriesAsync(lang, priority, cancellationToken);

        public Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>> GetCategoryLanguageGroupsAsync(RequestPriority priority, CancellationToken cancellationToken) =>
            _api.GetCategoryLanguageGroupsAsync(priority, cancellationToken);
    }
}
