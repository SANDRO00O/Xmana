using System;

namespace Xmana.Core.Playback
{
    public sealed class EngineFailureEventArgs : EventArgs
    {
        public EngineFailureEventArgs(PlayerFailure failure)
        {
            Failure = failure;
        }

        public PlayerFailure Failure { get; }
    }

    /// <summary>
    /// The platform media engine as the playback session sees it. The UWP implementation wraps <c>MediaPlayer</c>; tests use a fake.
    /// Events may be raised on any thread. Implementations must never log or persist <see cref="PlaybackSource.Media"/>.
    /// </summary>
    public interface IPlayerEngine
    {
        /// <summary>The stream opened and is ready to play.</summary>
        event EventHandler Opened;

        /// <summary>Playback reached the end of the media.</summary>
        event EventHandler Ended;

        event EventHandler<EngineFailureEventArgs> Failed;

        /// <summary>true when the engine started buffering, false when it resumed.</summary>
        event EventHandler<bool> BufferingChanged;

        /// <summary>Opens a stream and positions it at <paramref name="start"/>. Does not start playing.</summary>
        void Open(PlaybackSource source, TimeSpan start);

        void Play();

        void Pause();

        void Seek(TimeSpan position);

        /// <summary>Shows the given subtitle track, or none when null.</summary>
        void SelectSubtitle(Xmana.Core.Models.SubtitleLanguage? language);

        /// <summary>Stops playback and releases the stream.</summary>
        void Stop();

        TimeSpan Position { get; }

        /// <summary>Zero when not known yet.</summary>
        TimeSpan Duration { get; }
    }
}
