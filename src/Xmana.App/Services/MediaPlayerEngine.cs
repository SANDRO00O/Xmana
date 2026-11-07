using System;
using Windows.Media.Core;
using Windows.Media.Playback;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.App.Services
{
    /// <summary>
    /// <see cref="IPlayerEngine"/> over the platform <see cref="MediaPlayer"/> (hardware-accelerated HLS/MP4 on Xbox). Subtitle tracks are
    /// added as external timed-text sources and shown by the platform. Stream and subtitle addresses are handed straight to the
    /// platform and never logged, stored or exposed. Events come from background threads; the session is thread-safe.
    /// </summary>
    internal sealed class MediaPlayerEngine : IPlayerEngine, IDisposable
    {
        private readonly MediaPlayer _player;
        private readonly object _gate = new object();
        private MediaSource _source;
        private MediaPlaybackItem _item;
        private TimeSpan _pendingStart;
        private SubtitleLanguage? _subtitle;

        public MediaPlayerEngine()
        {
            _player = new MediaPlayer
            {
                AutoPlay = false,
                AudioCategory = MediaPlayerAudioCategory.Movie,
            };
            _player.MediaOpened += OnMediaOpened;
            _player.MediaEnded += (s, e) => Ended?.Invoke(this, EventArgs.Empty);
            _player.MediaFailed += OnMediaFailed;
            _player.PlaybackSession.BufferingStarted += (s, e) => BufferingChanged?.Invoke(this, true);
            _player.PlaybackSession.BufferingEnded += (s, e) => BufferingChanged?.Invoke(this, false);
        }

        public event EventHandler Opened;

        public event EventHandler Ended;

        public event EventHandler<EngineFailureEventArgs> Failed;

        public event EventHandler<bool> BufferingChanged;

        /// <summary>The platform player, for MediaPlayerElement.SetMediaPlayer.</summary>
        public MediaPlayer Player => _player;

        public TimeSpan Position
        {
            get
            {
                try
                {
                    return _player.PlaybackSession.Position;
                }
                catch (Exception)
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public TimeSpan Duration
        {
            get
            {
                try
                {
                    var duration = _player.PlaybackSession.NaturalDuration;
                    return duration < TimeSpan.Zero || duration > TimeSpan.FromDays(2) ? TimeSpan.Zero : duration;
                }
                catch (Exception)
                {
                    return TimeSpan.Zero;
                }
            }
        }

        public void Open(PlaybackSource source, TimeSpan start)
        {
            lock (_gate)
            {
                ReleaseCurrent();

                var media = MediaSource.CreateFromUri(source.Media);
                foreach (var subtitle in source.Subtitles)
                {
                    var language = subtitle.Language;
                    var timedText = TimedTextSource.CreateFromUri(subtitle.Uri);
                    timedText.Resolved += (s, e) =>
                    {
                        // The label is how a resolved track is matched back to its language.
                        if (e.Error == null && e.Tracks.Count > 0)
                        {
                            e.Tracks[0].Label = language.ToString();
                        }
                    };
                    media.ExternalTimedTextSources.Add(timedText);
                }

                var item = new MediaPlaybackItem(media);
                item.TimedMetadataTracksChanged += (s, e) => ApplySubtitle();

                _source = media;
                _item = item;
                _pendingStart = start;
                _player.Source = item;
            }
        }

        public void Play() => _player.Play();

        public void Pause() => _player.Pause();

        public void Seek(TimeSpan position)
        {
            try
            {
                _player.PlaybackSession.Position = position;
            }
            catch (Exception)
            {
                // seeking an unopened stream is a no-op
            }
        }

        public void SelectSubtitle(SubtitleLanguage? language)
        {
            lock (_gate)
            {
                _subtitle = language;
            }

            ApplySubtitle();
        }

        public void Stop()
        {
            lock (_gate)
            {
                try
                {
                    _player.Pause();
                }
                catch (Exception)
                {
                }

                ReleaseCurrent();
            }
        }

        public void Dispose()
        {
            Stop();
            _player.Dispose();
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            TimeSpan start;
            lock (_gate)
            {
                start = _pendingStart;
                _pendingStart = TimeSpan.Zero;
            }

            if (start > TimeSpan.Zero)
            {
                Seek(start);
            }

            ApplySubtitle();
            Opened?.Invoke(this, EventArgs.Empty);
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            PlayerFailure failure;
            switch (args.Error)
            {
                case MediaPlayerError.NetworkError:
                case MediaPlayerError.Aborted:
                    failure = PlayerFailure.Network;
                    break;
                case MediaPlayerError.DecodingError:
                    failure = PlayerFailure.Decoding;
                    break;
                case MediaPlayerError.SourceNotSupported:
                    failure = PlayerFailure.InvalidMedia;
                    break;
                default:
                    failure = PlayerFailure.Unknown;
                    break;
            }

            Failed?.Invoke(this, new EngineFailureEventArgs(failure));
        }

        private void ApplySubtitle()
        {
            lock (_gate)
            {
                var item = _item;
                if (item == null)
                {
                    return;
                }

                try
                {
                    var tracks = item.TimedMetadataTracks;
                    for (var i = 0; i < tracks.Count; i++)
                    {
                        var show = _subtitle != null && tracks[i].Label == _subtitle.Value.ToString();
                        tracks.SetPresentationMode((uint)i, show ? TimedMetadataTrackPresentationMode.PlatformPresented : TimedMetadataTrackPresentationMode.Disabled);
                    }
                }
                catch (Exception)
                {
                    // subtitle problems never interrupt playback
                }
            }
        }

        private void ReleaseCurrent()
        {
            _player.Source = null;
            _item = null;
            if (_source != null)
            {
                _source.Dispose();
                _source = null;
            }
        }
    }
}
