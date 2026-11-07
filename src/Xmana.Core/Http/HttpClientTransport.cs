using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Http
{
    /// <summary>
    /// <see cref="IHttpTransport"/> over System.Net.Http. The handler is injectable so tests can
    /// exercise real request building and body decoding without a network.
    /// </summary>
    public sealed class HttpClientTransport : IHttpTransport, IDisposable
    {
        private readonly HttpClient _client;

        public HttpClientTransport()
            : this(new HttpClientHandler())
        {
        }

        public HttpClientTransport(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler ?? throw new ArgumentNullException(nameof(handler)), true)
            {
                // Timeouts are enforced per attempt by ApiHttpClient so retries and cancellation stay consistent.
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
        }

        public async Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var method = request.Method == ApiHttpMethod.Post ? HttpMethod.Post : HttpMethod.Get;
            using (var message = new HttpRequestMessage(method, request.Uri))
            {
                message.Headers.TryAddWithoutValidation("Accept", "application/json");
                if (request.Body != null)
                {
                    message.Content = new StringContent(request.Body, Encoding.UTF8, request.ContentType ?? "application/json");
                }

                using (var response = await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    var charset = response.Content.Headers.ContentType != null ? response.Content.Headers.ContentType.CharSet : null;
                    return new TransportResponse((int)response.StatusCode, DecodeBody(bytes, charset));
                }
            }
        }

        public void Dispose() => _client.Dispose();

        /// <summary>
        /// Decodes a response body: honours a declared charset when the platform knows it, otherwise UTF-8
        /// (the JSON default), and drops a leading byte-order mark.
        /// </summary>
        public static string DecodeBody(byte[] bytes, string? charset)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            var encoding = ResolveEncoding(charset);
            var text = encoding.GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text.Substring(1) : text;
        }

        private static Encoding ResolveEncoding(string? charset)
        {
            if (!string.IsNullOrWhiteSpace(charset))
            {
                try
                {
                    return Encoding.GetEncoding(charset!.Trim().Trim('"'));
                }
                catch (ArgumentException)
                {
                    // unknown or unsupported charset name: fall back to UTF-8
                }
            }

            return new UTF8Encoding(false);
        }
    }
}
