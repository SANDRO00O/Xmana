using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Images;
using Xmana.Core.Library;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Home
{
    /// <summary>
    /// Presentation state of the Home page (PROMPT.md sections 20, 21, 57, 58).
    /// Sections load concurrently in priority order (hero first); each section succeeds or fails on its own, a failing
    /// section never blocks the others, and sections without items are simply absent (no fake content, no blank gaps).
    /// A refresh keeps what is on screen and swaps in new data as it arrives. Stale cache data served earlier is replaced
    /// quietly after the background refresh completes. The class holds no UI types; the UI listens to <see cref="Changed"/>.
    /// </summary>
    public sealed class HomeController
    {
        /// <summary>
        /// <c>groupsID</c> of the Featured Series group. Observed live behavior (API.md section 8), not an official constant,
        /// so the rail is optional: it exists only while the API returns a non-empty group with this id.
        /// </summary>
        public const string FeaturedSeriesGroupId = "2";

        private static readonly IReadOnlyList<VideoSummary> NoVideos = new VideoSummary[0];
        private static readonly IReadOnlyList<HomeSection> NoSections = new HomeSection[0];

        private readonly IHomeDataSource _source;
        private readonly Func<string> _language;
        private readonly IDelayer _delayer;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private readonly Slot[] _slots;
        private readonly HashSet<string> _loadingMore = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _nextPage = new Dictionary<string, int>(StringComparer.Ordinal);

        private IReadOnlyList<VideoSummary> _hero = NoVideos;
        private IReadOnlyList<HomeSection> _sections = NoSections;
        private HomeState _state = HomeState.Loading;
        private bool _outdated;
        private bool _quietReloadQueued;
        private Task _quietReload = Task.CompletedTask;

        private readonly Func<IReadOnlyList<ProgressItem>>? _continueWatching;

        public HomeController(
            IHomeDataSource source,
            Func<string> languageCode,
            IDelayer? delayer = null,
            IXmanaLogger? logger = null,
            Func<IReadOnlyList<ProgressItem>>? continueWatching = null)
        {
            _continueWatching = continueWatching;
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _language = languageCode ?? throw new ArgumentNullException(nameof(languageCode));
            _delayer = delayer ?? TaskDelayer.Instance;
            _logger = logger ?? NullLogger.Instance;

            // Order here is the order on screen. Priority is the order of network service (PROMPT.md section 58).
            _slots = new[]
            {
                new Slot("hero", RequestPriority.Hero, LoadHeroAsync),
                new Slot("continue", RequestPriority.ContinueWatching, (quiet, ct) => Task.FromResult(BuildContinueWatching())),
                new Slot("featuredMovies", RequestPriority.Primary, (quiet, ct) => LoadListAsync("featuredMovies", HomeSectionKind.FeaturedMovies, false, (p, t) => _source.GetFeaturedMoviesAsync(p, t), RequestPriority.Primary, ct)),
                new Slot("latestMovies", RequestPriority.Primary, (quiet, ct) => LoadListAsync("latestMovies", HomeSectionKind.LatestMovies, true, (p, t) => _source.GetLatestMoviesAsync(0, p, t), RequestPriority.Primary, ct)),
                new Slot("latestSeries", RequestPriority.Primary, (quiet, ct) => LoadListAsync("latestSeries", HomeSectionKind.LatestSeries, true, (p, t) => _source.GetLatestSeriesAsync(0, p, t), RequestPriority.Primary, ct)),
                new Slot("groups", RequestPriority.Primary, LoadGroupsAsync),
                new Slot("newly", RequestPriority.Secondary, (quiet, ct) => LoadListAsync("newlyAdded", HomeSectionKind.NewlyAdded, false, (p, t) => _source.GetNewlyAddedAsync(ApiHomeDataSource.NewlyAddedCount, p, t), RequestPriority.Secondary, ct)),
                new Slot("popular", RequestPriority.Secondary, (quiet, ct) => LoadListAsync("popular", HomeSectionKind.Popular, false, (p, t) => _source.GetPopularAsync(0, p, t), RequestPriority.Secondary, ct)),
            };

            _source.BackgroundRefreshed += OnBackgroundRefreshed;
        }

        /// <summary>Raised (possibly on a worker thread) whenever the visible state changed.</summary>
        public event EventHandler? Changed;

        public HomeState State
        {
            get
            {
                lock (_gate)
                {
                    return _state;
                }
            }
        }

        /// <summary>Banner items that have usable artwork (the hero never shows an empty backdrop).</summary>
        public IReadOnlyList<VideoSummary> Hero
        {
            get
            {
                lock (_gate)
                {
                    return _hero;
                }
            }
        }

        public IReadOnlyList<HomeSection> Sections
        {
            get
            {
                lock (_gate)
                {
                    return _sections;
                }
            }
        }

        /// <summary>True while any visible data is an older cached copy shown because the network failed.</summary>
        public bool IsShowingOutdatedData
        {
            get
            {
                lock (_gate)
                {
                    return _outdated;
                }
            }
        }

        /// <summary>Loads (or reloads) every section. Safe to call again for Retry, language change or refresh.</summary>
        public Task LoadAsync(CancellationToken cancellationToken) => LoadCoreAsync(false, cancellationToken);

        /// <summary>
        /// Re-reads the local Continue Watching list (after playing something, or when the page is shown again). Purely local: no
        /// request is made, so it is instant and works offline.
        /// </summary>
        public void RefreshLocal()
        {
            var result = BuildContinueWatching();
            lock (_gate)
            {
                var slot = _slots.First(s => s.Name == "continue");
                slot.Result = result;
                slot.Loaded = true;
                slot.Failed = false;
                RebuildLocked();
            }

            RaiseChanged();
        }

        private SlotResult BuildContinueWatching()
        {
            var items = _continueWatching == null ? new ProgressItem[0] : _continueWatching();
            if (items.Count == 0)
            {
                return new SlotResult(NoSections, null, DataOrigin.Network);
            }

            var progress = items.ToDictionary(i => i.Video.Id, i => i.Fraction, StringComparer.Ordinal);
            return new SlotResult(
                new[] { new HomeSection("continueWatching", HomeSectionKind.ContinueWatching, null, items.Select(i => i.Video).ToList(), false, null, progress) },
                null,
                DataOrigin.Network);
        }

        /// <summary>Requests the next page of a rail. Returns true when new items were added.</summary>
        public async Task<bool> LoadMoreAsync(string sectionId, CancellationToken cancellationToken)
        {
            HomeSection? section;
            int page;
            lock (_gate)
            {
                section = _sections.FirstOrDefault(s => s.Id == sectionId);
                if (section == null || !section.CanLoadMore || !_loadingMore.Add(sectionId))
                {
                    return false;
                }

                page = _nextPage.TryGetValue(sectionId, out var stored) ? stored : 1;
            }

            try
            {
                IReadOnlyList<VideoSummary> fetched;
                switch (section.Kind)
                {
                    case HomeSectionKind.LatestMovies:
                        fetched = (await _source.GetLatestMoviesAsync(page, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value;
                        break;
                    case HomeSectionKind.LatestSeries:
                        fetched = (await _source.GetLatestSeriesAsync(page, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value;
                        break;
                    case HomeSectionKind.Group:
                    case HomeSectionKind.FeaturedSeries:
                        if (string.IsNullOrEmpty(section.GroupId))
                        {
                            return false;
                        }

                        fetched = (await _source.GetGroupPageAsync(section.GroupId!, page, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value;
                        break;
                    default:
                        return false;
                }

                bool added;
                lock (_gate)
                {
                    var known = new HashSet<string>(section.Items.Select(i => i.Id), StringComparer.Ordinal);
                    var fresh = fetched.Where(v => known.Add(v.Id)).ToList();
                    added = fresh.Count > 0;
                    _nextPage[sectionId] = page + 1;

                    var merged = section.Items.Concat(fresh).ToList();
                    ReplaceSection(section.With(merged, added));
                    RebuildLocked();
                }

                RaiseChanged();
                return added;
            }
            catch (ApiException ex)
            {
                _logger.Warn("home: load more failed for " + (section.Kind) + " (" + ex.Kind + ")");
                return false; // CanLoadMore stays true so the user can try again
            }
            finally
            {
                lock (_gate)
                {
                    _loadingMore.Remove(sectionId);
                }
            }
        }

        /// <summary>Completes when a debounced background reload (if any) has finished. Used by tests and shutdown.</summary>
        public Task WaitForQuietReloadAsync()
        {
            lock (_gate)
            {
                return _quietReload;
            }
        }

        // ---- loading ---------------------------------------------------------------------------

        private async Task LoadCoreAsync(bool quiet, CancellationToken cancellationToken)
        {
            await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_gate)
                {
                    foreach (var slot in _slots)
                    {
                        slot.Loaded = false;
                        slot.Failed = false;
                    }

                    if (!quiet)
                    {
                        _nextPage.Clear();
                    }

                    RebuildLocked();
                }

                if (!quiet)
                {
                    RaiseChanged();
                }

                await Task.WhenAll(_slots.Select(slot => RunSlotAsync(slot, quiet, cancellationToken))).ConfigureAwait(false);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private async Task RunSlotAsync(Slot slot, bool quiet, CancellationToken cancellationToken)
        {
            SlotResult? result = null;
            var failed = false;
            try
            {
                result = await slot.Load(quiet, cancellationToken).ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                failed = true;
                _logger.Warn("home: section " + slot.Name + " failed (" + ex.Kind + (ex.StatusCode.HasValue ? " " + ex.StatusCode.Value : string.Empty) + ")");
            }

            lock (_gate)
            {
                slot.Loaded = true;
                slot.Failed = failed;
                if (result != null)
                {
                    slot.Result = quiet ? KeepUnchangedSections(slot.Result, result) : result;
                }

                RebuildLocked();
            }

            RaiseChanged();
        }

        private async Task<SlotResult> LoadHeroAsync(bool quiet, CancellationToken cancellationToken)
        {
            var response = await _source.GetBannerAsync(RequestPriority.Hero, cancellationToken).ConfigureAwait(false);
            var usable = response.Value.Where(v => ImageSelector.For(v, ImageRole.Banner) != null).ToList();
            return new SlotResult(NoSections, usable, response.Origin);
        }

        private async Task<SlotResult> LoadListAsync(
            string id,
            HomeSectionKind kind,
            bool pageable,
            Func<RequestPriority, CancellationToken, Task<ApiResult<IReadOnlyList<VideoSummary>>>> fetch,
            RequestPriority priority,
            CancellationToken cancellationToken)
        {
            var response = await fetch(priority, cancellationToken).ConfigureAwait(false);
            if (response.Value.Count == 0)
            {
                return new SlotResult(NoSections, null, response.Origin);
            }

            return new SlotResult(new[] { new HomeSection(id, kind, null, response.Value, pageable) }, null, response.Origin);
        }

        private async Task<SlotResult> LoadGroupsAsync(bool quiet, CancellationToken cancellationToken)
        {
            var response = await _source.GetGroupsAsync(_language(), RequestPriority.Primary, cancellationToken).ConfigureAwait(false);
            var sections = new List<HomeSection>();
            var featuredSeriesSeen = false;
            foreach (var group in response.Value)
            {
                if (group.Items.Count == 0)
                {
                    continue; // nothing to show
                }

                if (group.GroupId == FeaturedSeriesGroupId && !featuredSeriesSeen)
                {
                    // Featured Series is a regular group with a well-known id; it gets its own rail kind (and its API title,
                    // or a localized one when the API gives none) instead of appearing as an anonymous group.
                    featuredSeriesSeen = true;
                    var title = string.IsNullOrWhiteSpace(group.Title) ? null : group.Title;
                    sections.Add(new HomeSection("featuredSeries", HomeSectionKind.FeaturedSeries, title, group.Items, group.HasPagination == true, group.GroupId));
                    continue;
                }

                // A group without a title cannot be labelled.
                if (string.IsNullOrWhiteSpace(group.Title))
                {
                    continue;
                }

                sections.Add(new HomeSection("group:" + group.GroupId, HomeSectionKind.Group, group.Title, group.Items, group.HasPagination == true, group.GroupId));
            }

            return new SlotResult(sections, null, response.Origin);
        }

        // A quiet reload (after a background cache refresh) must not disturb what the user is browsing: a section whose
        // first-page items did not change keeps its current (possibly already extended) item list.
        private static SlotResult KeepUnchangedSections(SlotResult? previous, SlotResult fresh)
        {
            if (previous == null || fresh.Sections.Count == 0)
            {
                return fresh;
            }

            var merged = new List<HomeSection>();
            foreach (var section in fresh.Sections)
            {
                var old = previous.Sections.FirstOrDefault(s => s.Id == section.Id);
                var unchanged = old != null
                    && old.Items.Count >= section.Items.Count
                    && old.Items.Take(section.Items.Count).Select(v => v.Id).SequenceEqual(section.Items.Select(v => v.Id));
                merged.Add(unchanged ? old! : section);
            }

            return new SlotResult(merged, fresh.Hero, fresh.Origin);
        }

        // ---- state -----------------------------------------------------------------------------

        private void ReplaceSection(HomeSection replacement)
        {
            foreach (var slot in _slots)
            {
                if (slot.Result == null)
                {
                    continue;
                }

                var index = slot.Result.Sections.ToList().FindIndex(s => s.Id == replacement.Id);
                if (index >= 0)
                {
                    var list = slot.Result.Sections.ToList();
                    list[index] = replacement;
                    slot.Result = new SlotResult(list, slot.Result.Hero, slot.Result.Origin);
                    return;
                }
            }
        }

        private void RebuildLocked()
        {
            _hero = _slots[0].Result?.Hero ?? NoVideos;
            var ordered = _slots.SelectMany(s => s.Result?.Sections ?? NoSections).ToList();

            // Featured Series belongs right after Featured Movies (it arrives with the groups, which load later).
            var featuredSeries = ordered.FindIndex(s => s.Kind == HomeSectionKind.FeaturedSeries);
            if (featuredSeries >= 0)
            {
                var rail = ordered[featuredSeries];
                ordered.RemoveAt(featuredSeries);
                var movies = ordered.FindIndex(s => s.Kind == HomeSectionKind.FeaturedMovies);
                ordered.Insert(movies >= 0 ? movies + 1 : 0, rail);
            }

            _sections = ordered;
            _outdated = _slots.Any(s => s.Result != null && s.Result.Origin == DataOrigin.CacheAfterError);

            if (_hero.Count > 0 || _sections.Count > 0)
            {
                _state = HomeState.Content;
            }
            else if (_slots.All(s => s.Loaded))
            {
                _state = _slots.Any(s => s.Failed) ? HomeState.Error : HomeState.Empty;
            }
            else
            {
                _state = HomeState.Loading;
            }
        }

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

        private void OnBackgroundRefreshed(object? sender, string key)
        {
            lock (_gate)
            {
                if (_quietReloadQueued)
                {
                    return;
                }

                _quietReloadQueued = true;
                _quietReload = QuietReloadAsync();
            }
        }

        private async Task QuietReloadAsync()
        {
            await Task.Yield();
            try
            {
                await _delayer.DelayAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    _quietReloadQueued = false;
                }

                await LoadCoreAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ApiException || ex is OperationCanceledException)
            {
                lock (_gate)
                {
                    _quietReloadQueued = false;
                }
            }
        }

        // ---- types -----------------------------------------------------------------------------

        private sealed class SlotResult
        {
            public SlotResult(IReadOnlyList<HomeSection> sections, IReadOnlyList<VideoSummary>? hero, DataOrigin origin)
            {
                Sections = sections;
                Hero = hero;
                Origin = origin;
            }

            public IReadOnlyList<HomeSection> Sections { get; }

            public IReadOnlyList<VideoSummary>? Hero { get; }

            public DataOrigin Origin { get; }
        }

        private sealed class Slot
        {
            public Slot(string name, RequestPriority priority, Func<bool, CancellationToken, Task<SlotResult>> load)
            {
                Name = name;
                Priority = priority;
                Load = load;
            }

            public string Name { get; }

            public RequestPriority Priority { get; }

            public Func<bool, CancellationToken, Task<SlotResult>> Load { get; }

            public SlotResult? Result { get; set; }

            public bool Loaded { get; set; }

            public bool Failed { get; set; }
        }
    }
}
