using System;

namespace Xmana.Core.Http
{
    public enum ApiErrorKind
    {
        /// <summary>The request was refused locally (for example a non-HTTPS or non-allow-listed host).</summary>
        InvalidRequest,

        /// <summary>DNS, connection or other transport failure.</summary>
        Network,

        /// <summary>The per-attempt timeout elapsed.</summary>
        Timeout,

        /// <summary>The server answered with a non-success HTTP status.</summary>
        HttpStatus,

        /// <summary>The server answered 2xx but the body was not valid JSON.</summary>
        Malformed,
    }

    /// <summary>
    /// The only exception type the HTTP/API layer throws for remote failures. Messages never contain
    /// URLs; <see cref="Label"/> is a short endpoint name such as "allVideoInfo".
    /// Cancellation is never wrapped: it surfaces as <see cref="OperationCanceledException"/>.
    /// </summary>
    public sealed class ApiException : Exception
    {
        public ApiException(ApiErrorKind kind, string message, int? statusCode = null, string? label = null, Exception? inner = null)
            : base(message, inner)
        {
            Kind = kind;
            StatusCode = statusCode;
            Label = label;
        }

        public ApiErrorKind Kind { get; }

        public int? StatusCode { get; }

        public string? Label { get; }

        public bool IsRetryable =>
            Kind == ApiErrorKind.Network
            || Kind == ApiErrorKind.Timeout
            || (Kind == ApiErrorKind.HttpStatus && StatusCode.HasValue && RetryPolicy.IsRetryableStatus(StatusCode.Value));
    }
}
