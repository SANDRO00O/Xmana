using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Caching;
using Xmana.Core.Discovery;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Details
{
    /// <summary>
    /// State of a cast/crew screen (PROMPT.md section 29). The API returns the person plus associated titles in one response
    /// with no documented pagination, so there is no load-more.
    /// </summary>
    public sealed class StaffController
    {
        private readonly IDetailsDataSource _source;
        private readonly string _staffId;
        private readonly Func<string> _language;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();

        private ResultsState _state = ResultsState.Idle;
        private StaffMember? _staff;
        private IReadOnlyList<VideoSummary> _titles = new VideoSummary[0];
        private bool _outdated;
        private int _generation;

        public StaffController(IDetailsDataSource source, string staffId, Func<string>? languageCode = null, IXmanaLogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(staffId))
            {
                throw new ArgumentException("A staff id is required.", nameof(staffId));
            }

            _source = source ?? throw new ArgumentNullException(nameof(source));
            _staffId = staffId.Trim();
            _language = languageCode ?? (() => "en");
            _logger = logger ?? NullLogger.Instance;
        }

        public event EventHandler? Changed;

        public ResultsState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        public StaffMember? Staff
        {
            get
            {
                lock (_gate)
                {
                    return _staff;
                }
            }
        }

        public IReadOnlyList<VideoSummary> Titles
        {
            get
            {
                lock (_gate)
                {
                    return _titles;
                }
            }
        }

        /// <summary>The titles as a results snapshot so the shared grid can render them.</summary>
        public ResultsSnapshot Snapshot
        {
            get
            {
                lock (_gate)
                {
                    // "Empty" is only meaningful when there is neither a person nor titles.
                    var state = _state == ResultsState.Content && _titles.Count == 0 ? ResultsState.Empty : _state;
                    return new ResultsSnapshot(state, _titles, false, false, false, _outdated);
                }
            }
        }

        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            int generation;
            lock (_gate)
            {
                generation = ++_generation;
                _state = ResultsState.Loading;
            }

            RaiseChanged();

            try
            {
                var result = await _source.GetStaffAsync(_staffId, _language(), RequestPriority.Primary, cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    _staff = result.Value.Staff;
                    _titles = result.Value.Titles;
                    _outdated = result.Origin == DataOrigin.CacheAfterError;
                    _state = _staff == null && _titles.Count == 0 ? ResultsState.Empty : ResultsState.Content;
                }
            }
            catch (ApiException ex)
            {
                _logger.Warn("staff: load failed (" + ex.Kind + ")");
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

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
