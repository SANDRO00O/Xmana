using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>
    /// The brain of the player (PROMPT.md sections 39-47). Platform-neutral: it talks to an <see cref="IPlayerEngine"/>, so every
    /// rule below is unit-tested without a media stack.
    ///
    /// * <b>Preparation</b>: qualities (required), subtitles and skip data (optional) and the saved position are loaded; the stream
    ///   is chosen from the qualities THIS video offers.
    /// * <b>Recovery</b>: a failure is retried on the same quality (with a freshly requested URL, since signed URLs expire) and then
    ///   the session falls back through the other qualities, always resuming at the last known position. Never forever.
    /// * <b>Progress</b>: written periodically (not per position event), on pause, on exit and when the engine ends.
    /// * <b>Series</b>: near-end offer of the next episode with a cancellable countdown that respects the autoplay preference.
    /// * <b>Privacy</b>: stream and subtitle addresses live only inside <see cref="PlaybackSource"/> objects handed to the engine.
    ///   Nothing here logs, stores or exposes them.
    /// </summary>
    public sealed class PlaybackSession : IDisposable
    {
        /// <summary>Failures tolerated on one quality (each with a fresh URL) before falling back to another quality.</summary>
        public const int MaxAttemptsPerQuality = 2;

        private static readonly TimeSpan StableAfter = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MinimumSavedPosition = TimeSpan.FromSeconds(5);

        private readonly object _gate = new object();
        private readonly PlaybackContext _context;
        private readonly IPlaybackDataSource _data;
        private readonly IPlayerEngine _engine;
        private readonly IProgressStore _progress;
        private readonly PlayerPreferences _preferences;
        private readonly IClock _clock;
        private readonly IDelayer _delayer;
        private readonly IXmanaLogger _logger;
        private readonly SkipTimeUnit? _skipUnitOverride;
        private readonly SeekAccelerator _seek = new SeekAccelerator();
        private readonly AutoNextController _autoNext = new AutoNextController();
        private readonly HashSet<int> _failedHeights = new HashSet<int>();

        private PlayerState _state = PlayerState.Idle;
        private PlayerFailure _failure = PlayerFailure.None;
        private bool _buffering;
        private bool _recovering;
        private int? _fellBackFrom;
        private IReadOnlyList<VideoQuality> _qualities = new VideoQuality[0];
        private VideoQuality? _quality;
        private IReadOnlyList<SubtitleTrack> _subtitles = new SubtitleTrack[0];
        private IReadOnlyList<SubtitleSource> _subtitleSources = new SubtitleSource[0];
        private SubtitleLanguage? _subtitle;
        private SkipInfo? _skip;
        private PlaybackProgress? _saved;
        private TimeSpan _position = TimeSpan.Zero;
        private TimeSpan _duration = TimeSpan.Zero;
        private TimeSpan _openedAt = TimeSpan.Zero;
        private int _attemptsOnQuality;
        private bool _wantPlay = true;
        private DateTime _lastSaveUtc = DateTime.MinValue;
        private int _generation;
        private bool _disposed;

        public PlaybackSession(
            PlaybackContext context,
            IPlaybackDataSource data,
            IPlayerEngine engine,
            IProgressStore progress,
            PlayerPreferences preferences,
            IClock? clock = null,
            IDelayer? delayer = null,
            IXmanaLogger? logger = null,
            SkipTimeUnit? skipUnit = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
            _clock = clock ?? SystemClock.Instance;
            _delayer = delayer ?? TaskDelayer.Instance;
            _logger = logger ?? NullLogger.Instance;
            _skipUnitOverride = skipUnit;

            _engine.Opened += OnEngineOpened;
            _engine.Ended += OnEngineEnded;
            _engine.Failed += OnEngineFailed;
            _engine.BufferingChanged += OnEngineBuffering;
        }

        /// <summary>Raised (possibly on a worker thread) whenever something the UI shows changed. Not raised for plain position ticks.</summary>
        public event EventHandler? Changed;

        /// <summary>The next-episode countdown ran out with autoplay on: the UI should open the next episode.</summary>
        public event EventHandler? AdvanceRequested;

        public PlaybackContext Context => _context;

        // ---- observable state -------------------------------------------------------------------

        public PlayerState State => Read(() => _state);

        public PlayerFailure Failure => Read(() => _failure);

        public bool IsBuffering => Read(() => _buffering);

        /// <summary>A failure is being worked around (retry or quality fallback).</summary>
        public bool IsRecovering => Read(() => _recovering);

        public TimeSpan Position => Read(() => _position);

        public TimeSpan Duration => Read(() => _duration);

        public IReadOnlyList<VideoQuality> Qualities => Read(() => _qualities);

        public VideoQuality? CurrentQuality => Read(() => _quality);

        /// <summary>The height playback fell back from after repeated failures (null when no fallback happened).</summary>
        public int? FellBackFromHeight => Read(() => _fellBackFrom);

        /// <summary>Subtitle tracks that can actually be loaded (empty means: hide subtitle controls).</summary>
        public IReadOnlyList<SubtitleTrack> Subtitles => Read(() => _subtitles);

        public SubtitleLanguage? CurrentSubtitle => Read(() => _subtitle);

        public PlaybackProgress? SavedProgress => Read(() => _saved);

        /// <summary>True when a saved position is worth a Resume / Start Over prompt.</summary>
        public bool CanResume => Read(() => _preferences.ResumeEnabled && ProgressPolicy.IsResumable(_saved));

        public SkipSegment? ActiveSkip => Read(() => ComputeSkip(_position));

        public AutoNextPhase AutoNextPhase => Read(() => _autoNext.Phase);

        public int? AutoNextSecondsRemaining => Read(() => _autoNext.SecondsRemaining);

        /// <summary>The UI should show "Next episode in N" (last seconds, autoplay on).</summary>
        public bool AutoNextShowsCountdown => Read(() => _autoNext.ShowCountdown);

        // ---- lifecycle --------------------------------------------------------------------------

        /// <summary>Loads everything needed to play. Moves to <see cref="PlayerState.Ready"/> or <see cref="PlayerState.Failed"/>.</summary>
        public async Task PrepareAsync(CancellationToken cancellationToken)
        {
            int generation;
            lock (_gate)
            {
                generation = ++_generation;
                _state = PlayerState.Preparing;
                _failure = PlayerFailure.None;
                _saved = SafeGetProgress();
            }

            RaiseChanged();

            var qualitiesTask = LoadQualitiesAsync(cancellationToken);
            var translationsTask = Optional(() => _data.GetTranslationsAsync(_context.VideoId, cancellationToken));
            var skipTask = Optional(() => _data.GetSkipInfoAsync(_context.VideoId, cancellationToken));

            IReadOnlyList<VideoQuality> qualities;
            try
            {
                qualities = await qualitiesTask.ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                _logger.Warn("player: qualities failed (" + ex.Kind + (ex.StatusCode.HasValue ? " " + ex.StatusCode.Value : string.Empty) + ")");
                Fail(generation, PlayerFailure.Network);
                return;
            }

            var translations = await translationsTask.ConfigureAwait(false);
            var skip = await skipTask.ConfigureAwait(false);

            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                _qualities = qualities;
                _quality = QualitySelector.Choose(qualities, _preferences.PreferredQualityHeight);
                ApplyTranslations(translations);
                _skip = skip;

                if (_quality == null)
                {
                    _state = PlayerState.Failed;
                    _failure = PlayerFailure.NoSource;
                }
                else
                {
                    _state = PlayerState.Ready;
                }
            }

            RaiseChanged();
        }

        /// <summary>Starts playback (state must be Ready). <see cref="PlaybackStartMode.Resume"/> uses the saved position when it is meaningful.</summary>
        public void Start(PlaybackStartMode mode)
        {
            TimeSpan start;
            lock (_gate)
            {
                if (_state != PlayerState.Ready)
                {
                    return;
                }

                start = mode == PlaybackStartMode.Resume && _preferences.ResumeEnabled && ProgressPolicy.IsResumable(_saved) ? ProgressPolicy.ResumePosition(_saved!) : TimeSpan.Zero;
                _wantPlay = true;
                _autoNext.Reset();
            }

            OpenCurrent(start);
        }

        /// <summary>Starts over from the failure screen: requests fresh stream addresses and resumes at the last position.</summary>
        public async Task RetryAsync(CancellationToken cancellationToken)
        {
            int generation;
            TimeSpan resumeAt;
            bool haveQualities;
            lock (_gate)
            {
                if (_state != PlayerState.Failed)
                {
                    return;
                }

                generation = ++_generation;
                resumeAt = _position;
                haveQualities = _qualities.Count > 0;
                _failedHeights.Clear();
                _attemptsOnQuality = 0;
                _fellBackFrom = null;
                _failure = PlayerFailure.None;
                _state = PlayerState.Preparing;
            }

            RaiseChanged();

            if (!haveQualities)
            {
                await PrepareAsync(cancellationToken).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_state != PlayerState.Ready)
                    {
                        return;
                    }

                    _wantPlay = true;
                }

                OpenCurrent(resumeAt);
                return;
            }

            await RefreshQualitiesAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                _quality = QualitySelector.Choose(_qualities, _preferences.PreferredQualityHeight);
                _wantPlay = true;
            }

            OpenCurrent(resumeAt);
        }

        /// <summary>Saves progress and stops the engine. Call when leaving the player.</summary>
        public void Stop()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _generation++;
            }

            Flush();
            _engine.Stop();
            lock (_gate)
            {
                _state = PlayerState.Idle;
            }

            RaiseChanged();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _generation++;
            }

            _engine.Opened -= OnEngineOpened;
            _engine.Ended -= OnEngineEnded;
            _engine.Failed -= OnEngineFailed;
            _engine.BufferingChanged -= OnEngineBuffering;
        }

        // ---- transport --------------------------------------------------------------------------

        public void Play()
        {
            lock (_gate)
            {
                if (_state == PlayerState.Opening)
                {
                    _wantPlay = true;
                    return;
                }

                if (_state != PlayerState.Paused)
                {
                    return;
                }

                _state = PlayerState.Playing;
                _wantPlay = true;
            }

            _engine.Play();
            RaiseChanged();
        }

        public void Pause()
        {
            lock (_gate)
            {
                if (_state == PlayerState.Opening)
                {
                    _wantPlay = false;
                    return;
                }

                if (_state != PlayerState.Playing)
                {
                    return;
                }

                _state = PlayerState.Paused;
                _wantPlay = false;
            }

            _engine.Pause();
            Flush();
            RaiseChanged();
        }

        /// <summary>A button / A press: pause, resume, or replay after the end.</summary>
        public void TogglePlayPause()
        {
            PlayerState state;
            lock (_gate)
            {
                state = _state;
            }

            switch (state)
            {
                case PlayerState.Playing:
                    Pause();
                    break;
                case PlayerState.Paused:
                case PlayerState.Opening:
                    if (state == PlayerState.Opening)
                    {
                        lock (_gate)
                        {
                            _wantPlay = !_wantPlay;
                        }
                    }
                    else
                    {
                        Play();
                    }

                    break;
                case PlayerState.Ended:
                    lock (_gate)
                    {
                        _position = TimeSpan.Zero;
                        _state = PlayerState.Playing;
                        _wantPlay = true;
                        _autoNext.Reset();
                    }

                    _engine.Seek(TimeSpan.Zero);
                    _engine.Play();
                    RaiseChanged();
                    break;
            }
        }

        /// <summary>One Left/Right press (<paramref name="direction"/> -1 or +1); repeated presses accelerate.</summary>
        public void SeekBy(int direction)
        {
            TimeSpan target;
            lock (_gate)
            {
                if (_state != PlayerState.Playing && _state != PlayerState.Paused && _state != PlayerState.Ended)
                {
                    return;
                }

                target = Clamp(_position + _seek.Step(direction, _clock.UtcNow));
                _position = target;
                if (_state == PlayerState.Ended)
                {
                    _state = PlayerState.Paused;
                }
            }

            _engine.Seek(target);
            RaiseChanged();
        }

        public void SeekTo(TimeSpan position)
        {
            TimeSpan target;
            lock (_gate)
            {
                if (_state != PlayerState.Playing && _state != PlayerState.Paused && _state != PlayerState.Ended)
                {
                    return;
                }

                target = Clamp(position);
                _position = target;
                _seek.Reset();
                if (_state == PlayerState.Ended)
                {
                    _state = PlayerState.Paused;
                }
            }

            _engine.Seek(target);
            RaiseChanged();
        }

        public void SkipIntro()
        {
            SkipSegment? segment;
            lock (_gate)
            {
                segment = ComputeSkip(_position);
            }

            if (segment != null)
            {
                SeekTo(segment.End);
            }
        }

        // ---- ticks and progress -----------------------------------------------------------------

        /// <summary>Called by the UI a couple of times per second with the engine's position. Cheap; writes progress only periodically.</summary>
        public void Tick(TimeSpan position, TimeSpan duration)
        {
            var changed = false;
            var advance = false;
            var save = false;
            lock (_gate)
            {
                if (_state != PlayerState.Playing && _state != PlayerState.Paused && _state != PlayerState.Ended)
                {
                    return;
                }

                _position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
                if (duration > TimeSpan.Zero)
                {
                    _duration = duration;
                }

                var now = _clock.UtcNow;

                if (_state == PlayerState.Playing && _position - _openedAt >= StableAfter)
                {
                    _attemptsOnQuality = 0; // playing steadily: an old failure no longer counts against this quality
                    if (_recovering)
                    {
                        _recovering = false;
                        changed = true;
                    }
                }

                changed |= _autoNext.Update(_position, _duration, _context.Next != null, _preferences.AutoplayNext);
                if (_autoNext.ShouldAdvance)
                {
                    advance = true;
                }

                if (_state == PlayerState.Playing && now - _lastSaveUtc >= ProgressPolicy.SaveInterval)
                {
                    save = true;
                    _lastSaveUtc = now;
                }
            }

            if (save)
            {
                Flush();
            }

            if (changed)
            {
                RaiseChanged();
            }

            if (advance)
            {
                Flush();
                AdvanceRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>Writes the current progress now (pause, exit, app suspension). Safe to call at any time.</summary>
        public void Flush()
        {
            PlaybackProgress? record = null;
            lock (_gate)
            {
                var completed = _state == PlayerState.Ended || ProgressPolicy.IsCompleted(_position, _duration);
                if (_position >= MinimumSavedPosition || completed)
                {
                    record = new PlaybackProgress(
                        _context.VideoId,
                        _context.SeriesId,
                        _context.Season,
                        _context.Episode,
                        completed && _duration > TimeSpan.Zero ? _duration : _position,
                        _duration,
                        _clock.UtcNow,
                        completed);
                }
            }

            if (record != null)
            {
                try
                {
                    _progress.Save(record);
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                {
                    _logger.Warn("player: progress could not be saved");
                }
            }
        }

        public void CancelAutoNext()
        {
            lock (_gate)
            {
                _autoNext.Cancel();
            }

            RaiseChanged();
        }

        // ---- quality and subtitles --------------------------------------------------------------

        /// <summary>The viewer picked a quality: remember it, reopen that stream and keep the position and play/pause intent.</summary>
        public async Task SelectQualityAsync(VideoQuality quality, CancellationToken cancellationToken)
        {
            int generation;
            TimeSpan position;
            lock (_gate)
            {
                // Choosing a quality from the failure screen (PROMPT.md section 59 Player Error) is a deliberate retry at that
                // quality, same as a plain Retry; everything else needs an open or playable session.
                if (quality == null || _state == PlayerState.Idle || _state == PlayerState.Preparing)
                {
                    return;
                }

                generation = ++_generation;
                position = _position;
                _wantPlay = _state != PlayerState.Paused;
                _failedHeights.Clear();
                _attemptsOnQuality = 0;
                _fellBackFrom = null;
                _recovering = false;
                _failure = PlayerFailure.None;
                _preferences.PreferredQualityHeight = quality.Height;
            }

            await RefreshQualitiesAsync(cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                _quality = _qualities.FirstOrDefault(q => q.Height == quality.Height) ?? quality;
            }

            OpenCurrent(position);
        }

        /// <summary>Shows a subtitle track, or turns subtitles off with null. Unavailable tracks are ignored. The choice is remembered.</summary>
        public void SelectSubtitle(SubtitleLanguage? language)
        {
            lock (_gate)
            {
                if (language != null && _subtitleSources.All(s => s.Language != language.Value))
                {
                    return;
                }

                _subtitle = language;
                _preferences.Subtitle = PlayerPreferences.From(language);
            }

            _engine.SelectSubtitle(language);
            RaiseChanged();
        }

        // ---- engine events ----------------------------------------------------------------------

        private void OnEngineOpened(object? sender, EventArgs e)
        {
            bool play;
            SubtitleLanguage? subtitle;
            lock (_gate)
            {
                if (_state != PlayerState.Opening)
                {
                    return;
                }

                play = _wantPlay;
                subtitle = _subtitle;
                _state = play ? PlayerState.Playing : PlayerState.Paused;
            }

            _engine.SelectSubtitle(subtitle);
            if (play)
            {
                _engine.Play();
            }

            RaiseChanged();
        }

        private void OnEngineEnded(object? sender, EventArgs e)
        {
            var advance = false;
            lock (_gate)
            {
                if (_state != PlayerState.Playing && _state != PlayerState.Paused)
                {
                    return;
                }

                _state = PlayerState.Ended;
                if (_duration > TimeSpan.Zero)
                {
                    _position = _duration;
                }

                _autoNext.OnEnded(_context.Next != null, _preferences.AutoplayNext);
                advance = _autoNext.ShouldAdvance;
            }

            Flush();
            RaiseChanged();
            if (advance)
            {
                AdvanceRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnEngineBuffering(object? sender, bool buffering)
        {
            lock (_gate)
            {
                if (_buffering == buffering)
                {
                    return;
                }

                _buffering = buffering;
            }

            RaiseChanged();
        }

        private void OnEngineFailed(object? sender, EngineFailureEventArgs e)
        {
            int generation;
            TimeSpan position;
            VideoQuality? failedQuality;
            VideoQuality? target = null;
            var retrySame = false;
            lock (_gate)
            {
                // Only a stream that is opening or playing can fail; anything else (ready, ended, failed, idle) is not our concern.
                if (_disposed || (_state != PlayerState.Opening && _state != PlayerState.Playing && _state != PlayerState.Paused))
                {
                    return;
                }

                failedQuality = _quality;
                position = _position;
                _attemptsOnQuality++;

                // Network and unknown problems may be transient or caused by an expired address; a stream the engine cannot decode is not.
                var retryable = e.Failure == PlayerFailure.Network || e.Failure == PlayerFailure.Unknown;
                if (retryable && _attemptsOnQuality < MaxAttemptsPerQuality)
                {
                    retrySame = true;
                }
                else if (failedQuality != null)
                {
                    _failedHeights.Add(failedQuality.Height);
                    target = QualitySelector.Fallback(_qualities, failedQuality, _failedHeights);
                }

                if (!retrySame && target == null)
                {
                    _generation++;
                    _state = PlayerState.Failed;
                    _failure = e.Failure == PlayerFailure.None ? PlayerFailure.Unknown : e.Failure;
                    _recovering = false;
                    _buffering = false;
                    generation = _generation;
                }
                else
                {
                    generation = ++_generation;
                    _state = PlayerState.Opening;
                    _recovering = true;
                    if (target != null && failedQuality != null)
                    {
                        _fellBackFrom = failedQuality.Height;
                        _quality = target;
                        _attemptsOnQuality = 0;
                    }
                }
            }

            _logger.Warn("player: failure " + e.Failure + (retrySame ? ", retrying" : target != null ? ", falling back" : ", giving up"));

            if (!retrySame && target == null)
            {
                Flush();
                RaiseChanged();
                return;
            }

            var heightToReopen = (target ?? failedQuality)!.Height;
            RaiseChanged();
            var ignored = RecoverAsync(generation, heightToReopen, position);
        }

        private async Task RecoverAsync(int generation, int height, TimeSpan position)
        {
            try
            {
                // A short, growing pause so a flapping connection is not hammered.
                await _delayer.DelayAsync(TimeSpan.FromMilliseconds(600 * Math.Max(1, _attemptsOnQuality + 1)), CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }
                }

                // Signed addresses expire: ask again, then reopen the chosen height from the fresh list.
                await RefreshQualitiesAsync(CancellationToken.None).ConfigureAwait(false);
                lock (_gate)
                {
                    if (generation != _generation)
                    {
                        return;
                    }

                    var fresh = _qualities.FirstOrDefault(q => q.Height == height);
                    if (fresh != null)
                    {
                        _quality = fresh;
                    }
                    else if (_quality == null || _qualities.All(q => q.Height != _quality.Height))
                    {
                        _quality = QualitySelector.Choose(_qualities, height);
                    }

                    if (_quality == null)
                    {
                        _state = PlayerState.Failed;
                        _failure = PlayerFailure.NoSource;
                        _recovering = false;
                    }
                }

                if (State == PlayerState.Failed)
                {
                    RaiseChanged();
                    return;
                }

                OpenCurrent(position);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                _logger.Error("player: recovery failed (" + ex.GetType().Name + ")");
                Fail(generation, PlayerFailure.Unknown);
            }
        }

        // ---- internals --------------------------------------------------------------------------

        private void OpenCurrent(TimeSpan start)
        {
            PlaybackSource source;
            lock (_gate)
            {
                if (_quality == null)
                {
                    _state = PlayerState.Failed;
                    _failure = PlayerFailure.NoSource;
                    source = null!;
                }
                else
                {
                    _state = PlayerState.Opening;
                    _position = start;
                    _openedAt = start;
                    _lastSaveUtc = _clock.UtcNow;
                    _seek.Reset();
                    source = new PlaybackSource(new Uri(_quality.Url), _quality.Label, _subtitleSources);
                }
            }

            if (source == null)
            {
                RaiseChanged();
                return;
            }

            RaiseChanged();
            _engine.Open(source, start);
        }

        // An async wrapper, so a data source that throws before returning a task still ends up as a failure of this task.
        private async Task<IReadOnlyList<VideoQuality>> LoadQualitiesAsync(CancellationToken cancellationToken) =>
            await _data.GetQualitiesAsync(_context.VideoId, cancellationToken).ConfigureAwait(false);

        private async Task RefreshQualitiesAsync(CancellationToken cancellationToken)
        {
            try
            {
                var fresh = await _data.GetQualitiesAsync(_context.VideoId, cancellationToken).ConfigureAwait(false);
                if (fresh.Count > 0)
                {
                    lock (_gate)
                    {
                        _qualities = fresh;
                    }
                }
            }
            catch (ApiException ex)
            {
                // Keep the addresses we have; if they are expired the next failure moves on to the next step.
                _logger.Warn("player: refresh failed (" + ex.Kind + ")");
            }
        }

        private void ApplyTranslations(TranslationInfo? translations)
        {
            var tracks = new List<SubtitleTrack>();
            var sources = new List<SubtitleSource>();
            if (translations != null)
            {
                foreach (var track in translations.Tracks)
                {
                    Uri uri;
                    if (track.TryGetUri(out uri) && sources.All(s => s.Language != track.Language))
                    {
                        tracks.Add(track);
                        sources.Add(new SubtitleSource(track.Language, uri));
                    }
                }
            }

            _subtitles = tracks;
            _subtitleSources = sources;

            // Only the viewer's own remembered choice is applied, and only when that language really exists.
            var preferred = PlayerPreferences.ToLanguage(_preferences.Subtitle);
            _subtitle = preferred != null && sources.Any(s => s.Language == preferred.Value) ? preferred : null;
        }

        private SkipSegment? ComputeSkip(TimeSpan position) =>
            !_preferences.SkipIntroEnabled ? null : SkipController.Active(_skip, _context.HasIntroSkipping, _skipUnitOverride ?? SkipController.VerifiedUnit, position);

        private TimeSpan Clamp(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (_duration > TimeSpan.Zero)
            {
                var max = _duration - TimeSpan.FromSeconds(1);
                if (max < TimeSpan.Zero)
                {
                    max = TimeSpan.Zero;
                }

                return position > max ? max : position;
            }

            return position;
        }

        private PlaybackProgress? SafeGetProgress()
        {
            try
            {
                return _progress.Get(_context.VideoId);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private async Task<T?> Optional<T>(Func<Task<T>> call)
            where T : class
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                _logger.Warn("player: optional data failed (" + ex.Kind + ")");
                return null;
            }
        }

        private void Fail(int generation, PlayerFailure failure)
        {
            lock (_gate)
            {
                if (generation != _generation)
                {
                    return;
                }

                _state = PlayerState.Failed;
                _failure = failure;
                _recovering = false;
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
