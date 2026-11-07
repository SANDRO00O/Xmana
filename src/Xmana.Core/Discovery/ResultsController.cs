using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Discovery
{
    public enum ResultsState
    {
        /// <summary>Nothing has been requested yet.</summary>
        Idle,
        Loading,
        Content,

        /// <summary>The request succeeded and returned nothing.</summary>
        Empty,

        /// <summary>The first page could not be loaded.</summary>
        Error,
    }

    /// <summary>
    /// What a page fetcher is asked for. <see cref="PageIndex"/> counts pages from 0; <see cref="ItemsLoaded"/> is the
    /// number of items already shown. Each endpoint uses the field that matches ITS pagination model:
    /// page-based endpoints use PageIndex, offset-based endpoints use ItemsLoaded (PROMPT.md section 80).
    /// </summary>
    public sealed class PageRequest
    {
        public PageRequest(int pageIndex, int itemsLoaded)
        {
            PageIndex = pageIndex;
            ItemsLoaded = itemsLoaded;
        }

        public int PageIndex { get; }

        public int ItemsLoaded { get; }

        public RequestPriority Priority => PageIndex == 0 ? RequestPriority.Primary : RequestPriority.Secondary;
    }

    /// <summary>A consistent copy of everything a results screen needs to render.</summary>
    public sealed class ResultsSnapshot
    {
        public static readonly ResultsSnapshot Idle = new ResultsSnapshot(ResultsState.Idle, new VideoSummary[0], false, false, false, false);

        public ResultsSnapshot(ResultsState state, IReadOnlyList<VideoSummary> items, bool canLoadMore, bool isLoadingMore, bool loadMoreFailed, bool outdated)
        {
            State = state;
            Items = items;
            CanLoadMore = canLoadMore;
            IsLoadingMore = isLoadingMore;
            LoadMoreFailed = loadMoreFailed;
            Outdated = outdated;
        }

        public ResultsState State { get; }

        public IReadOnlyList<VideoSummary> Items { get; }

        public bool CanLoadMore { get; }

        public bool IsLoadingMore { get; }

        public bool LoadMoreFailed { get; }

        /// <summary>The shown items are an older cached copy because the network failed.</summary>
        public bool Outdated { get; }
    }

    public delegate Task<ApiResult<IReadOnlyList<VideoSummary>>> PageFetcher(PageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// A growing list of videos with loading, empty and error states and load-more. Used by browsing, category results,
    /// search and advanced search. Results of a superseded request are dropped, de-duplication spans pages, and pagination
    /// ends when a page adds nothing new.
    /// </summary>
    public sealed class ResultsController
    {
        private static readonly IReadOnlyList<VideoSummary> None = new VideoSummary[0];

        private readonly PageFetcher _fetch;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();

        private IReadOnlyList<VideoSummary> _items = None;
        private ResultsState _state = ResultsState.Idle;
        private bool _canLoadMore;
        private bool _loadingMore;
        private bool _loadMoreFailed;
        private bool _outdated;
        private int _pagesLoaded;
        private int _generation;

        public ResultsController(PageFetcher fetch, IXmanaLogger? logger = null)
        {
            _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
            _logger = logger ?? NullLogger.Instance;
        }

        public event EventHandler? Changed;

        public ResultsState State => Read(() => _state);

        public IReadOnlyList<VideoSummary> Items => Read(() => _items);

        public bool CanLoadMore => Read(() => _canLoadMore);

        public bool IsLoadingMore => Read(() => _loadingMore);

        /// <summary>The last load-more attempt failed; the shown items are intact and another attempt is allowed.</summary>
        public bool LoadMoreFailed => Read(() => _loadMoreFailed);

        public bool IsShowingOutdatedData => Read(() => _outdated);

        public ResultsSnapshot Snapshot
        {
            get
            {
                lock (_gate)
                {
                    return new ResultsSnapshot(_state, _items, _canLoadMore, _loadingMore, _loadMoreFailed, _outdated);
                }
            }
        }

        /// <summary>(Re)starts from the first page. A call made while another is running supersedes it.</summary>
        public async Task LoadFirstAsync(CancellationToken cancellationToken)
        {
            int generation;
            lock (_gate)
            {
                generation = ++_generation;
                _state = ResultsState.Loading;
                _items = None;
                _canLoadMore = false;
                _loadingMore = false;
                _loadMoreFailed = false;
                _outdated = false;
                _pagesLoaded = 0;
            }

            RaiseChanged();

            try
            {
                var result = await _fetch(new PageRequest(0, 0), cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    _items = result.Value;
                    _pagesLoaded = 1;
                    _canLoadMore = result.Value.Count > 0;
                    _outdated = result.Origin == DataOrigin.CacheAfterError;
                    _state = result.Value.Count == 0 ? ResultsState.Empty : ResultsState.Content;
                }
            }
            catch (ApiException ex)
            {
                _logger.Warn("results: first page failed (" + ex.Kind + (ex.StatusCode.HasValue ? " " + ex.StatusCode.Value : string.Empty) + ")");
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    _state = ResultsState.Error;
                }
            }

            RaiseChanged();
        }

        /// <summary>Loads the next page. Returns true when new items were added.</summary>
        public async Task<bool> LoadMoreAsync(CancellationToken cancellationToken)
        {
            int generation;
            PageRequest request;
            lock (_gate)
            {
                if (_state != ResultsState.Content || !_canLoadMore || _loadingMore)
                {
                    return false;
                }

                _loadingMore = true;
                _loadMoreFailed = false;
                generation = _generation;
                request = new PageRequest(_pagesLoaded, _items.Count);
            }

            RaiseChanged();

            var added = false;
            try
            {
                var result = await _fetch(request, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return false;
                    }

                    var known = new HashSet<string>(_items.Select(i => i.Id), StringComparer.Ordinal);
                    var fresh = result.Value.Where(v => known.Add(v.Id)).ToList();
                    added = fresh.Count > 0;
                    _pagesLoaded++;
                    if (added)
                    {
                        _items = _items.Concat(fresh).ToList();
                    }

                    _canLoadMore = added;
                }
            }
            catch (ApiException ex)
            {
                _logger.Warn("results: load more failed (" + ex.Kind + ")");
                lock (_gate)
                {
                    if (generation == _generation)
                    {
                        _loadMoreFailed = true;
                    }
                }
            }
            finally
            {
                lock (_gate)
                {
                    if (generation == _generation)
                    {
                        _loadingMore = false;
                    }
                }
            }

            RaiseChanged();
            return added;
        }

        private T Read<T>(Func<T> getter)
        {
            lock (_gate)
            {
                return getter();
            }
        }

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
