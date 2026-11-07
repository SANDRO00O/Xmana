using System;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Logging;

namespace Xmana.Core.Discovery
{
    /// <summary>
    /// Drives a browse screen (Movies, Series, Anime, a category). Chooses the endpoint from the query and keeps that
    /// endpoint's own pagination model (PROMPT.md sections 23, 24, 80):
    /// no category -> generic listing, page based; category -> <c>videosByCategory</c>, offset based;
    /// category + language -> <c>videosByCategoryAndLanguage</c>, offset based.
    /// </summary>
    public sealed class BrowseController
    {
        private readonly IDiscoveryDataSource _source;
        private readonly IXmanaLogger _logger;
        private ResultsController _results;
        private BrowseQuery _query;

        public BrowseController(IDiscoveryDataSource source, BrowseQuery initial, IXmanaLogger? logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? NullLogger.Instance;
            _query = initial ?? throw new ArgumentNullException(nameof(initial));
            _results = Create(_query);
        }

        /// <summary>Raised when the query changed or the current results changed.</summary>
        public event EventHandler? Changed;

        public BrowseQuery Query => _query;

        public ResultsController Results => _results;

        /// <summary>Applies a query and loads its first page. Unchanged queries do not reload (unless already failed).</summary>
        public async Task ApplyAsync(BrowseQuery query, CancellationToken cancellationToken)
        {
            if (query == null)
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (query.Equals(_query) && _results.State != ResultsState.Idle && _results.State != ResultsState.Error)
            {
                return;
            }

            _query = query;
            _results = Create(query);
            Changed?.Invoke(this, EventArgs.Empty);
            await _results.LoadFirstAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task RetryAsync(CancellationToken cancellationToken) => _results.LoadFirstAsync(cancellationToken);

        public Task<bool> LoadMoreAsync(CancellationToken cancellationToken) => _results.LoadMoreAsync(cancellationToken);

        private ResultsController Create(BrowseQuery query)
        {
            PageFetcher fetch;
            if (query.CategoryId == null)
            {
                fetch = (request, ct) => _source.GetListingAsync(query.Kind, query.Sort, DiscoveryDefaults.ListingPageSize, request.PageIndex, request.Priority, ct);
            }
            else if (query.LanguageId == null)
            {
                var categoryId = query.CategoryId;
                fetch = (request, ct) => _source.GetByCategoryAsync(categoryId, query.Kind, query.Sort, request.ItemsLoaded, request.Priority, ct);
            }
            else
            {
                var categoryId = query.CategoryId;
                var languageId = query.LanguageId;
                fetch = (request, ct) => _source.GetByCategoryAndLanguageAsync(categoryId, languageId, query.Kind, query.Sort, request.ItemsLoaded, request.Priority, ct);
            }

            var controller = new ResultsController(fetch, _logger);
            controller.Changed += (sender, e) =>
            {
                if (ReferenceEquals(sender, _results))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            };
            return controller;
        }
    }
}
