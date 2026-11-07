using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;
using Xmana.Core.Logging;

namespace Xmana.Core.Http
{
    /// <summary>
    /// The single place that talks to the network (PROMPT.md section 65): host validation, a prioritised
    /// concurrency slot, per-attempt timeout, bounded retry with backoff (GET only), HTTP status
    /// mapping, JSON parsing and safe logging. Callers see only <see cref="ApiException"/>, or
    /// <see cref="OperationCanceledException"/> when they cancelled.
    /// </summary>
    public sealed class ApiHttpClient
    {
        private readonly IHttpTransport _transport;
        private readonly HttpClientOptions _options;
        private readonly IXmanaLogger _logger;
        private readonly IDelayer _delayer;
        private readonly PriorityConcurrencyLimiter _limiter;

        public ApiHttpClient(
            IHttpTransport transport,
            HttpClientOptions? options = null,
            IXmanaLogger? logger = null,
            IDelayer? delayer = null,
            PriorityConcurrencyLimiter? limiter = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _options = options ?? new HttpClientOptions();
            _logger = logger ?? NullLogger.Instance;
            _delayer = delayer ?? TaskDelayer.Instance;
            _limiter = limiter ?? new PriorityConcurrencyLimiter(_options.MaxConcurrentRequests);
        }

        /// <param name="label">Short endpoint name used for logging and errors (never a URL).</param>
        public Task<JToken> GetJsonAsync(
            string label,
            Uri uri,
            RequestPriority priority = RequestPriority.Primary,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendJsonAsync(label, new TransportRequest(ApiHttpMethod.Get, uri), priority, cancellationToken);
        }

        /// <summary>POST is never retried automatically (unsafe operations must not be repeated).</summary>
        public Task<JToken> PostJsonAsync(
            string label,
            Uri uri,
            string jsonBody,
            RequestPriority priority = RequestPriority.Secondary,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return SendJsonAsync(label, new TransportRequest(ApiHttpMethod.Post, uri, jsonBody, "application/json"), priority, cancellationToken);
        }

        private async Task<JToken> SendJsonAsync(string label, TransportRequest request, RequestPriority priority, CancellationToken cancellationToken)
        {
            if (!UrlValidator.IsAllowedApiUri(request.Uri))
            {
                throw new ApiException(ApiErrorKind.InvalidRequest, "Request blocked: destination is not an allowed HTTPS API host.", null, label);
            }

            var body = await SendWithPolicyAsync(label, request, priority, cancellationToken).ConfigureAwait(false);

            try
            {
                return JsonParser.Parse(body);
            }
            catch (JsonException ex)
            {
                _logger.Warn(label + ": malformed JSON in response");
                throw new ApiException(ApiErrorKind.Malformed, "The server returned malformed JSON.", null, label, ex);
            }
        }

        private async Task<string> SendWithPolicyAsync(string label, TransportRequest request, RequestPriority priority, CancellationToken cancellationToken)
        {
            var maxAttempts = request.Method == ApiHttpMethod.Get ? _options.RetryPolicy.MaxAttempts : 1;

            using (await _limiter.AcquireAsync(priority, cancellationToken).ConfigureAwait(false))
            {
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var stopwatch = Stopwatch.StartNew();
                    ApiException? error = null;

                    try
                    {
                        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                        {
                            linked.CancelAfter(_options.Timeout);
                            var response = await _transport.SendAsync(request, linked.Token).ConfigureAwait(false);

                            if (response.StatusCode >= 200 && response.StatusCode <= 299)
                            {
                                _logger.Debug(label + " -> " + response.StatusCode + " in " + stopwatch.ElapsedMilliseconds + " ms (attempt " + attempt + ")");
                                return response.Body;
                            }

                            error = new ApiException(ApiErrorKind.HttpStatus, "HTTP status " + response.StatusCode + ".", response.StatusCode, label);
                        }
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        // Our own timeout fired (the caller did not cancel).
                        error = new ApiException(ApiErrorKind.Timeout, "The request timed out.", null, label);
                    }
                    catch (Exception ex) when (IsTransient(ex))
                    {
                        error = new ApiException(ApiErrorKind.Network, "Network error (" + ex.GetType().Name + ").", null, label, ex);
                    }

                    if (error == null)
                    {
                        throw new InvalidOperationException("Request finished without a result.");
                    }

                    _logger.Warn(label + " failed: " + error.Kind + (error.StatusCode.HasValue ? " " + error.StatusCode.Value : string.Empty) + " (attempt " + attempt + " of " + maxAttempts + ")");

                    if (attempt < maxAttempts && error.IsRetryable)
                    {
                        await _delayer.DelayAsync(_options.RetryPolicy.GetDelay(attempt), cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw error;
                }
            }

            // Unreachable: the loop either returns or throws.
            throw new ApiException(ApiErrorKind.Network, "The request did not complete.", null, label);
        }

        private static bool IsTransient(Exception ex) =>
            ex is HttpRequestException || ex is IOException || ex is WebException || ex is SocketException;
    }
}
