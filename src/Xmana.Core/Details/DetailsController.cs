using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Details
{
    public enum DetailsState
    {
        Loading,
        Content,

        /// <summary>The request worked but returned no usable video (unknown or removed id).</summary>
        NotFound,

        /// <summary>The metadata request failed.</summary>
        Error,
    }

    public enum SeriesLoadState
    {
        /// <summary>Not a series (or not loaded yet): nothing to show.</summary>
        None,
        Loading,
        Loaded,

        /// <summary>Seasons and episodes could not be loaded; the rest of the screen still works.</summary>
        Failed,
    }

    /// <summary>
    /// State of the details screen for one movie, series or episode (PROMPT.md sections 17, 31, 37).
    /// The metadata request decides whether the screen works at all; everything else (episodes, related titles,
    /// recommendations, series name) loads independently and may fail or be empty without affecting the rest.
    /// </summary>
    public sealed class DetailsController
    {
        /// <summary>
        /// API.md section 4 verified <c>relatedVideos</c> only with <c>kind/1</c>; the meaning of other values is unknown. The
        /// video's own kind is tried first and the verified value is the fallback when that returns nothing.
        /// </summary>
        public const string VerifiedRelatedKind = "1";

        private static readonly IReadOnlyList<VideoSummary> NoVideos = new VideoSummary[0];

        private readonly IDetailsDataSource _source;
        private readonly Func<string> _language;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();

        private DetailsState _state = DetailsState.Loading;
        private VideoDetails? _details;
        private bool _outdated;
        private SeriesLoadState _seriesState = SeriesLoadState.None;
        private SeriesContext? _series;
        private string? _seriesId;
        private string? _seriesTitleEn;
        private string? _seriesTitleAr;
        private IReadOnlyList<VideoSummary> _related = NoVideos;
        private IReadOnlyList<VideoSummary> _recommended = NoVideos;
        private int _generation;

        public DetailsController(IDetailsDataSource source, string videoId, Func<string>? languageCode = null, IXmanaLogger? logger = null)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException("A video id is required.", nameof(videoId));
            }

            _source = source ?? throw new ArgumentNullException(nameof(source));
            VideoId = videoId.Trim();
            _language = languageCode ?? (() => "en");
            _logger = logger ?? NullLogger.Instance;
        }

        public event EventHandler? Changed;

        public string VideoId { get; private set; }

        public DetailsState State => Read(() => _state);

        public VideoDetails? Details => Read(() => _details);

        public bool IsShowingOutdatedData => Read(() => _outdated);

        public SeriesLoadState SeriesState => Read(() => _seriesState);

        public SeriesContext? Series => Read(() => _series);

        /// <summary>True when the video belongs to a series (its <c>rootSeries</c> is set and not "0").</summary>
        public bool IsEpisode => Read(() => _details != null && IsRealId(_details.Summary.RootSeriesId));

        /// <summary>Series title when this is an episode and the series metadata could be loaded.</summary>
        public string? GetSeriesTitle(bool preferArabic) => Read(() =>
        {
            var preferred = preferArabic ? _seriesTitleAr : _seriesTitleEn;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : (preferArabic ? _seriesTitleEn : _seriesTitleAr);
        });

        public IReadOnlyList<VideoSummary> Related => Read(() => _related);

        public IReadOnlyList<VideoSummary> Recommended => Read(() => _recommended);

        /// <summary>Loads everything (also used for Retry). A newer call makes an older one stop publishing.</summary>
        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            int generation;
            lock (_gate)
            {
                generation = ++_generation;
                if (_details == null)
                {
                    _state = DetailsState.Loading;
                }
            }

            RaiseChanged();

            VideoDetails? details;
            DataOrigin origin;
            try
            {
                var result = await _source.GetVideoInfoAsync(VideoId, RequestPriority.Hero, cancellationToken).ConfigureAwait(false);
                details = result.Value;
                origin = result.Origin;
            }
            catch (ApiException ex)
            {
                _logger.Warn("details: metadata failed (" + ex.Kind + (ex.StatusCode.HasValue ? " " + ex.StatusCode.Value : string.Empty) + ")");
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    // Keep what is already on screen when only a refresh failed.
                    if (_details == null)
                    {
                        _state = DetailsState.Error;
                    }
                }

                RaiseChanged();
                return;
            }

            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                if (details == null)
                {
                    _state = DetailsState.NotFound;
                }
                else
                {
                    _details = details;
                    _state = DetailsState.Content;
                    _outdated = origin == DataOrigin.CacheAfterError;
                }
            }

            RaiseChanged();
            if (details == null)
            {
                return;
            }

            await Task.WhenAll(
                LoadSeriesAsync(details, generation, cancellationToken),
                LoadSeriesTitleAsync(details, generation, cancellationToken),
                LoadRelatedAsync(details, generation, cancellationToken),
                LoadRecommendationsAsync(details, generation, cancellationToken)).ConfigureAwait(false);
        }

        /// <summary>
        /// Switches this same screen to another video (PROMPT.md UI/UX redesign: episode selection must update the page in place,
        /// never navigate/reload the whole screen). A no-op when <paramref name="videoId"/> is already showing. Previously shown
        /// content stays on screen while the new metadata loads (same "no flash" behaviour as a background refresh).
        /// </summary>
        public Task SwitchToAsync(string videoId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(videoId))
            {
                throw new ArgumentException("A video id is required.", nameof(videoId));
            }

            var trimmed = videoId.Trim();
            lock (_gate)
            {
                if (trimmed == VideoId)
                {
                    return Task.CompletedTask;
                }

                VideoId = trimmed;
            }

            return LoadAsync(cancellationToken);
        }

        /// <summary>Retries only the seasons and episodes (after <see cref="SeriesLoadState.Failed"/>).</summary>
        public Task RetrySeriesAsync(CancellationToken cancellationToken)
        {
            VideoDetails? details;
            int generation;
            lock (_gate)
            {
                details = _details;
                generation = _generation;
            }

            return details == null ? Task.CompletedTask : LoadSeriesAsync(details, generation, cancellationToken);
        }

        /// <summary>Shows another season's episodes. Unknown seasons are ignored.</summary>
        public void SelectSeason(int? season)
        {
            lock (_gate)
            {
                if (_series == null || _series.Groups.All(g => g.Season != season))
                {
                    return;
                }

                _series = _series.WithSeason(season, SeriesContext.FlatOrder(_series.Groups));
            }

            RaiseChanged();
        }

        // ---- sections --------------------------------------------------------------------------

        private async Task LoadSeriesAsync(VideoDetails details, int generation, CancellationToken cancellationToken)
        {
            if (details.Summary.Kind != VideoKind.Series)
            {
                return; // movies and unknown kinds have no seasons
            }

            var seriesId = IsRealId(details.Summary.RootSeriesId) ? details.Summary.RootSeriesId! : details.Id;
            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                _seriesId = seriesId;
                _seriesState = SeriesLoadState.Loading;
            }

            RaiseChanged();

            IReadOnlyList<VideoSummary>? episodes = null;
            IReadOnlyList<SeasonInfo>? seasons = null;
            var episodesTask = Attempt(async () => episodes = (await _source.GetEpisodesAsync(seriesId, RequestPriority.Primary, cancellationToken).ConfigureAwait(false)).Value, "episodes");
            var seasonsTask = Attempt(async () => seasons = (await _source.GetSeasonsAsync(seriesId, RequestPriority.Primary, cancellationToken).ConfigureAwait(false)).Value, "seasons");
            var episodesOk = await episodesTask.ConfigureAwait(false);
            var seasonsOk = await seasonsTask.ConfigureAwait(false);

            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                if (!episodesOk && !seasonsOk)
                {
                    _seriesState = SeriesLoadState.Failed;
                }
                else
                {
                    var current = IsRealId(details.Summary.RootSeriesId) ? details.Id : null; // only episodes have a "current" one
                    var context = SeriesContext.Build(
                        seriesId,
                        episodes ?? NoVideos,
                        (seasons ?? new SeasonInfo[0]).Select(s => s.Number),
                        current);
                    _series = context;
                    _seriesState = SeriesLoadState.Loaded;
                }
            }

            RaiseChanged();
        }

        private async Task LoadSeriesTitleAsync(VideoDetails details, int generation, CancellationToken cancellationToken)
        {
            if (!IsRealId(details.Summary.RootSeriesId))
            {
                return;
            }

            try
            {
                var series = (await _source.GetVideoInfoAsync(details.Summary.RootSeriesId!, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value;
                if (series == null)
                {
                    return;
                }

                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    _seriesTitleEn = series.Summary.EnTitle;
                    _seriesTitleAr = series.Summary.ArTitle;
                }

                RaiseChanged();
            }
            catch (ApiException ex)
            {
                _logger.Warn("details: series title failed (" + ex.Kind + ")"); // purely decorative
            }
        }

        private async Task LoadRelatedAsync(VideoDetails details, int generation, CancellationToken cancellationToken)
        {
            IReadOnlyList<VideoSummary> items = NoVideos;
            try
            {
                var ownKind = string.IsNullOrWhiteSpace(details.Summary.KindRaw) ? VerifiedRelatedKind : details.Summary.KindRaw!;
                items = Without(details.Id, (await _source.GetRelatedAsync(details.Id, ownKind, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value);
                if (items.Count == 0 && ownKind != VerifiedRelatedKind)
                {
                    items = Without(details.Id, (await _source.GetRelatedAsync(details.Id, VerifiedRelatedKind, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false)).Value);
                }
            }
            catch (ApiException ex)
            {
                _logger.Warn("details: related failed (" + ex.Kind + ")");
            }

            Publish(generation, () => _related = items);
        }

        private async Task LoadRecommendationsAsync(VideoDetails details, int generation, CancellationToken cancellationToken)
        {
            IReadOnlyList<VideoSummary> items = NoVideos;
            try
            {
                var title = details.Summary.EnTitle ?? details.Summary.ArTitle ?? string.Empty;
                var result = await _source.GetRecommendationsAsync(details.Id, title, RequestPriority.Secondary, cancellationToken).ConfigureAwait(false);

                // Only items that are real video objects can be shown; the rest of the recommendation payload is ignored.
                items = Without(details.Id, result.Items.Where(i => i.Video != null).Select(i => i.Video!).ToList());
            }
            catch (ApiException ex)
            {
                // The service is optional (PROMPT.md section 31): the related section above remains the fallback.
                _logger.Warn("details: recommendations failed (" + ex.Kind + ")");
            }

            Publish(generation, () => _recommended = items);
        }

        // ---- helpers ---------------------------------------------------------------------------

        private static IReadOnlyList<VideoSummary> Without(string id, IReadOnlyList<VideoSummary> items) =>
            items.Where(v => v.Id != id).ToList();

        private static bool IsRealId(string? id) => !string.IsNullOrWhiteSpace(id) && id!.Trim() != "0";

        private async Task<bool> Attempt(Func<Task> call, string label)
        {
            try
            {
                await call().ConfigureAwait(false);
                return true;
            }
            catch (ApiException ex)
            {
                _logger.Warn("details: " + label + " failed (" + ex.Kind + ")");
                return false;
            }
        }

        private void Publish(int generation, Action apply)
        {
            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                apply();
            }

            RaiseChanged();
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
