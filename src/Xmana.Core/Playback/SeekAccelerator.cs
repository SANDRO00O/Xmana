using System;

namespace Xmana.Core.Playback
{
    /// <summary>
    /// Turns repeated Left/Right presses into accelerated steps (PROMPT.md section 42): 10 s at first, then 20, 30 and 60 s
    /// while the viewer keeps pressing in the same direction. A pause in input or a direction change starts over.
    /// </summary>
    public sealed class SeekAccelerator
    {
        private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(700);

        private int _direction;
        private int _count;
        private DateTime _last = DateTime.MinValue;

        /// <summary>The signed step for one press (<paramref name="direction"/> is -1 or +1).</summary>
        public TimeSpan Step(int direction, DateTime nowUtc)
        {
            if (direction == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }

            var sign = direction < 0 ? -1 : 1;
            if (sign == _direction && nowUtc - _last <= Window && nowUtc >= _last)
            {
                _count++;
            }
            else
            {
                _count = 1;
            }

            _direction = sign;
            _last = nowUtc;
            return TimeSpan.FromSeconds(sign * SecondsFor(_count));
        }

        public void Reset()
        {
            _direction = 0;
            _count = 0;
            _last = DateTime.MinValue;
        }

        private static int SecondsFor(int count)
        {
            if (count <= 2)
            {
                return 10;
            }

            if (count <= 4)
            {
                return 20;
            }

            return count <= 7 ? 30 : 60;
        }
    }
}
