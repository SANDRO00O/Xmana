using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Discovery
{
    /// <summary>
    /// Title search (PROMPT.md section 25). Typing is debounced so no request is sent per keystroke, the latest text always
    /// wins (older results never replace newer ones), an identical repeated query is not re-sent, and clearing the text
    /// returns to the idle state. <see cref="SubmitAsync"/> searches immediately (keyboard Enter / on-screen "Search").
    /// </summary>
    public sealed class SearchController
    {
        private readonly IDiscoveryDataSource _source;
        private readonly IDelayer _delayer;
        private readonly TimeSpan _debounce;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();

        private ResultsController? _results;
        private string _text = string.Empty;
        private string _executed = string.Empty;
        private int _schedule;
        private Task _pending = Task.CompletedTask;

        public SearchController(IDiscoveryDataSource source, IDelayer? delayer = null, TimeSpan? debounce = null, IXmanaLogger? logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _delayer = delayer ?? TaskDelayer.Instance;
            _debounce = debounce ?? DiscoveryDefaults.SearchDebounce;
            _logger = logger ?? NullLogger.Instance;
        }

        public event EventHandler? Changed;

        /// <summary>The trimmed text last given to <see cref="SetText"/> (what the box currently holds).</summary>
        public string Text
        {
            get
            {
                lock (_gate)
                {
                    return _text;
                }
            }
        }

        /// <summary>The query whose results are shown.</summary>
        public string ExecutedQuery
        {
            get
            {
                lock (_gate)
                {
                    return _executed;
                }
            }
        }

        public ResultsSnapshot Snapshot => Current?.Snapshot ?? ResultsSnapshot.Idle;

        public ResultsState State => Current?.State ?? ResultsState.Idle;

        public IReadOnlyList<VideoSummary> Items => Current?.Items ?? new VideoSummary[0];

        public bool CanLoadMore => Current?.CanLoadMore ?? false;

        public bool LoadMoreFailed => Current?.LoadMoreFailed ?? false;

        public bool IsLoadingMore => Current?.IsLoadingMore ?? false;

        private ResultsController? Current
        {
            get
            {
                lock (_gate)
                {
                    return _results;
                }
            }
        }

        /// <summary>Call for every text change. Only the text that is still current after the debounce period is searched.</summary>
        public void SetText(string? text)
        {
            var trimmed = (text ?? string.Empty).Trim();
            int ticket;
            lock (_gate)
            {
                if (string.Equals(trimmed, _text, StringComparison.Ordinal))
                {
                    return;
                }

                _text = trimmed;
                ticket = ++_schedule;
                if (trimmed.Length == 0)
                {
                    _results = null;
                    _executed = string.Empty;
                    RaiseAfterUnlock();
                    return;
                }

                _pending = RunDebouncedAsync(trimmed, ticket);
            }
        }

        /// <summary>Searches right now (no debounce). Blank text just clears the results.</summary>
        public async Task SubmitAsync(string? text, CancellationToken cancellationToken)
        {
            var trimmed = (text ?? string.Empty).Trim();
            int ticket;
            lock (_gate)
            {
                _text = trimmed;
                ticket = ++_schedule;
            }

            if (trimmed.Length == 0)
            {
                lock (_gate)
                {
                    _results = null;
                    _executed = string.Empty;
                }

                Changed?.Invoke(this, EventArgs.Empty);
                return;
            }

            await ExecuteAsync(trimmed, ticket, true, cancellationToken).ConfigureAwait(false);
        }

        public Task RetryAsync(CancellationToken cancellationToken)
        {
            string text;
            int ticket;
            lock (_gate)
            {
                text = _text;
                ticket = ++_schedule;
            }

            return text.Length == 0 ? Task.CompletedTask : ExecuteAsync(text, ticket, true, cancellationToken);
        }

        public Task<bool> LoadMoreAsync(CancellationToken cancellationToken)
        {
            var results = Current;
            return results == null ? Task.FromResult(false) : results.LoadMoreAsync(cancellationToken);
        }

        /// <summary>Completes when the latest debounced search (if any) has finished. Used by tests and shutdown.</summary>
        public Task WaitForPendingAsync()
        {
            lock (_gate)
            {
                return _pending;
            }
        }

        private async Task RunDebouncedAsync(string text, int ticket)
        {
            await Task.Yield();
            try
            {
                await _delayer.DelayAsync(_debounce, CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    if (ticket != _schedule)
                    {
                        return; // the user kept typing (or cleared the box)
                    }
                }

                await ExecuteAsync(text, ticket, false, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // superseded
            }
        }

        private async Task ExecuteAsync(string text, int ticket, bool force, CancellationToken cancellationToken)
        {
            ResultsController results;
            lock (_gate)
            {
                if (ticket != _schedule)
                {
                    return;
                }

                var sameAsShown = string.Equals(text, _executed, StringComparison.OrdinalIgnoreCase)
                    && _results != null
                    && (_results.State == ResultsState.Content || _results.State == ResultsState.Empty || _results.State == ResultsState.Loading);
                if (!force && sameAsShown)
                {
                    return;
                }

                var keyword = text;
                results = new ResultsController(
                    (request, ct) => _source.SearchAsync(keyword, DiscoveryDefaults.SearchPageSize, request.PageIndex, request.Priority, ct),
                    _logger);
                results.Changed += (sender, e) =>
                {
                    bool current;
                    lock (_gate)
                    {
                        current = ReferenceEquals(sender, _results);
                    }

                    if (current)
                    {
                        Changed?.Invoke(this, EventArgs.Empty);
                    }
                };
                _results = results;
                _executed = text;
            }

            await results.LoadFirstAsync(cancellationToken).ConfigureAwait(false);
        }

        private void RaiseAfterUnlock()
        {
            // Called while the gate is held: raise on a continuation so handlers never run inside the lock.
            Task.Run(() => Changed?.Invoke(this, EventArgs.Empty));
        }
    }
}
