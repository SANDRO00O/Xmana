using System;

namespace Xmana.Core.Playback
{
    public enum AutoNextPhase
    {
        Hidden,

        /// <summary>The next episode is offered (play now / cancel). With autoplay on, playback moves on when the media ends.</summary>
        Offering,
    }

    /// <summary>
    /// Near-the-end behaviour for series (PROMPT.md section 44): detect near-completion, offer the next episode, show a countdown,
    /// allow cancel, never force autoplay. The countdown is simply the remaining playback time, so it reaches zero exactly when the
    /// media ends and a pause or a seek keeps it honest. Pure logic with no clock.
    /// </summary>
    public sealed class AutoNextController
    {
        /// <summary>The offer appears when this much of the episode is left.</summary>
        public static readonly TimeSpan OfferWindow = TimeSpan.FromSeconds(25);

        /// <summary>The visible "in N seconds" countdown starts when this much is left (autoplay on only).</summary>
        public static readonly TimeSpan CountdownWindow = TimeSpan.FromSeconds(10);

        private bool _offering;
        private bool _cancelled;
        private bool _advanceRaised;
        private int? _seconds;
        private bool _showCountdown;

        public AutoNextPhase Phase => _offering ? AutoNextPhase.Offering : AutoNextPhase.Hidden;

        /// <summary>Whole seconds of playback left while autoplay is on; null when autoplay is off or nothing is offered.</summary>
        public int? SecondsRemaining => _seconds;

        /// <summary>True when the UI should show "Next episode in N" (the last few seconds with autoplay on).</summary>
        public bool ShowCountdown => _showCountdown;

        /// <summary>True exactly once: autoplay is on and the episode reached its end.</summary>
        public bool ShouldAdvance { get; private set; }

        public bool IsCancelled => _cancelled;

        /// <summary>Evaluates one position tick. Returns true when anything visible changed.</summary>
        public bool Update(TimeSpan position, TimeSpan duration, bool hasNext, bool autoplayEnabled)
        {
            var before = Snapshot();
            ShouldAdvance = false;

            if (!hasNext || duration <= TimeSpan.Zero)
            {
                Clear();
                return before != Snapshot();
            }

            var remaining = duration - position;
            if (remaining > OfferWindow)
            {
                // Seeked back out of the window: a cancelled offer may appear again later and a finished countdown restarts.
                _cancelled = false;
                _advanceRaised = false;
                Clear();
                return before != Snapshot();
            }

            if (_cancelled)
            {
                return before != Snapshot();
            }

            _offering = true;
            if (autoplayEnabled)
            {
                var seconds = (int)Math.Max(0, Math.Ceiling(remaining.TotalSeconds));
                _seconds = seconds;
                _showCountdown = remaining <= CountdownWindow;
                if (remaining <= TimeSpan.Zero && !_advanceRaised)
                {
                    _advanceRaised = true;
                    ShouldAdvance = true;
                }
            }
            else
            {
                _seconds = null;
                _showCountdown = false;
            }

            return before != Snapshot();
        }

        /// <summary>The engine reported the end of the media.</summary>
        public void OnEnded(bool hasNext, bool autoplayEnabled)
        {
            ShouldAdvance = false;
            if (!hasNext || _cancelled)
            {
                return;
            }

            _offering = true;
            if (autoplayEnabled)
            {
                _seconds = 0;
                _showCountdown = true;
                if (!_advanceRaised)
                {
                    _advanceRaised = true;
                    ShouldAdvance = true;
                }
            }
            else
            {
                _seconds = null;
                _showCountdown = false;
            }
        }

        /// <summary>The viewer dismissed the offer; it stays hidden until they seek away from the end and back.</summary>
        public void Cancel()
        {
            _cancelled = true;
            Clear();
        }

        public void Reset()
        {
            _cancelled = false;
            _advanceRaised = false;
            Clear();
        }

        private void Clear()
        {
            _offering = false;
            _seconds = null;
            _showCountdown = false;
        }

        private string Snapshot() => _offering + "|" + _seconds + "|" + _showCountdown;
    }
}
