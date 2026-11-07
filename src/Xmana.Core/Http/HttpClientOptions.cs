using System;

namespace Xmana.Core.Http
{
    public sealed class HttpClientOptions
    {
        /// <summary>Timeout of a single attempt (not of the whole retried operation).</summary>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);

        public RetryPolicy RetryPolicy { get; set; } = new RetryPolicy();

        /// <summary>Upper bound of simultaneous in-flight requests (PROMPT.md section 57).</summary>
        public int MaxConcurrentRequests { get; set; } = 4;
    }
}
