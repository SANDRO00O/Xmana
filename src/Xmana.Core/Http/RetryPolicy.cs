using System;

namespace Xmana.Core.Http
{
    /// <summary>
    /// Bounded retry with exponential backoff. Only safe (GET) requests are ever retried; the HTTP
    /// client enforces that. Never retries forever.
    /// </summary>
    public sealed class RetryPolicy
    {
        public RetryPolicy(int maxAttempts = 3, TimeSpan? baseDelay = null, TimeSpan? maxDelay = null)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            MaxAttempts = maxAttempts;
            BaseDelay = baseDelay ?? TimeSpan.FromMilliseconds(400);
            MaxDelay = maxDelay ?? TimeSpan.FromSeconds(4);
        }

        public int MaxAttempts { get; }

        public TimeSpan BaseDelay { get; }

        public TimeSpan MaxDelay { get; }

        /// <summary>Delay to wait after the given failed attempt (1-based): base, 2x, 4x ... capped.</summary>
        public TimeSpan GetDelay(int failedAttempt)
        {
            if (failedAttempt < 1)
            {
                failedAttempt = 1;
            }

            var exponent = Math.Min(failedAttempt - 1, 20);
            var ticks = BaseDelay.Ticks * (1L << exponent);
            if (ticks < 0 || ticks > MaxDelay.Ticks)
            {
                ticks = MaxDelay.Ticks;
            }

            return TimeSpan.FromTicks(ticks);
        }

        public static bool IsRetryableStatus(int statusCode) =>
            statusCode == 408 || statusCode == 429 || (statusCode >= 500 && statusCode <= 599);
    }
}
