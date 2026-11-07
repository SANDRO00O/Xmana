using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Discovery
{
    /// <summary>
    /// Advanced search (PROMPT.md section 26). Filters are sent only when selected; an empty or invalid selection is never
    /// sent. Pagination is the endpoint's own zero-based <c>page</c>. The controller does not promise sorting or exact staff
    /// matching: API.md documents both as unreliable.
    /// </summary>
    public sealed class AdvancedSearchController
    {
        private readonly IDiscoveryDataSource _source;
        private readonly IXmanaLogger _logger;
        private ResultsController? _results;

        public AdvancedSearchController(IDiscoveryDataSource source, IXmanaLogger? logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? NullLogger.Instance;
        }

        public event EventHandler? Changed;

        public ResultsSnapshot Snapshot => _results?.Snapshot ?? ResultsSnapshot.Idle;

        public ResultsState State => _results?.State ?? ResultsState.Idle;

        public IReadOnlyList<VideoSummary> Items => _results?.Items ?? new VideoSummary[0];

        public bool CanLoadMore => _results?.CanLoadMore ?? false;

        public bool LoadMoreFailed => _results?.LoadMoreFailed ?? false;

        /// <summary>
        /// Runs the search. Returns false (and sends nothing) when the filters have no criteria or are invalid, so the screen
        /// can tell the user what is missing instead of listing everything.
        /// </summary>
        public async Task<bool> ApplyAsync(AdvancedSearchFilters filters, CancellationToken cancellationToken)
        {
            if (filters == null)
            {
                throw new ArgumentNullException(nameof(filters));
            }

            if (!filters.IsValid || !filters.HasCriteria)
            {
                return false;
            }

            var snapshot = filters.WithPage(0); // later edits to the form must not change pagination of this search
            var results = new ResultsController(
                (request, ct) => _source.AdvancedSearchAsync(snapshot.WithPage(request.PageIndex), request.Priority, ct),
                _logger);
            results.Changed += (sender, e) =>
            {
                if (ReferenceEquals(sender, _results))
                {
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            };
            _results = results;
            await results.LoadFirstAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public void Reset()
        {
            _results = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public Task<bool> LoadMoreAsync(CancellationToken cancellationToken) =>
            _results == null ? Task.FromResult(false) : _results.LoadMoreAsync(cancellationToken);

        public Task RetryAsync(CancellationToken cancellationToken) =>
            _results == null ? Task.CompletedTask : _results.LoadFirstAsync(cancellationToken);
    }
}
