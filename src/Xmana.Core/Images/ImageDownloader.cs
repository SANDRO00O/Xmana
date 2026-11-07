using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Http;
using Xmana.Core.Logging;

namespace Xmana.Core.Images
{
    public enum ImageFailure
    {
        InvalidUrl,
        Network,
        HttpStatus,
        TooLarge,
        NotAnImage,
    }

    /// <summary>Failure of an image download. Messages never contain the URL.</summary>
    public sealed class ImageDownloadException : Exception
    {
        public ImageDownloadException(ImageFailure failure, string message, Exception? inner = null)
            : base(message, inner)
        {
            Failure = failure;
        }

        public ImageFailure Failure { get; }

        public bool IsRetryable => Failure == ImageFailure.Network;
    }

    /// <summary>
    /// Downloads image bytes over HTTPS only, with a size cap, a per-attempt timeout and a signature check.
    /// Cancellation surfaces as <see cref="OperationCanceledException"/>. Retries are bounded.
    /// </summary>
    public sealed class ImageDownloader : IDisposable
    {
        public const long DefaultMaxBytes = 6L * 1024 * 1024;

        private readonly HttpClient _client;
        private readonly long _maxBytes;
        private readonly TimeSpan _timeout;
        private readonly int _attempts;
        private readonly IDelayer _delayer;
        private readonly IXmanaLogger _logger;
        private readonly PriorityConcurrencyLimiter _limiter;

        /// <summary>Simultaneous image downloads (PROMPT.md section 57): enough to keep rails filling, few enough to leave the API its share.</summary>
        public const int DefaultMaxConcurrent = 4;

        public ImageDownloader(HttpMessageHandler? handler = null, long maxBytes = DefaultMaxBytes, TimeSpan? timeout = null, int attempts = 2, IDelayer? delayer = null, IXmanaLogger? logger = null, int maxConcurrent = DefaultMaxConcurrent)
        {
            _client = new HttpClient(handler ?? new HttpClientHandler(), true) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            _maxBytes = maxBytes;
            _timeout = timeout ?? TimeSpan.FromSeconds(20);
            _attempts = Math.Max(1, attempts);
            _delayer = delayer ?? TaskDelayer.Instance;
            _logger = logger ?? NullLogger.Instance;
            _limiter = new PriorityConcurrencyLimiter(maxConcurrent);
        }

        public Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken) =>
            DownloadAsync(url, cancellationToken, RequestPriority.Low);

        /// <summary>Downloads an image. A more important <paramref name="priority"/> (hero artwork) is served first when slots are scarce.</summary>
        public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken, RequestPriority priority)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uri uri;
            if (!UrlValidator.TryGetHttpsUri(url, out uri))
            {
                throw new ImageDownloadException(ImageFailure.InvalidUrl, "Image URL is not an absolute HTTPS URL.");
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    using (await _limiter.AcquireAsync(priority, cancellationToken).ConfigureAwait(false))
                    {
                        return await DownloadOnceAsync(uri, cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (ImageDownloadException ex) when (ex.IsRetryable && attempt < _attempts)
                {
                    _logger.Debug("image download failed (" + ex.Failure + "), retrying");
                    await _delayer.DelayAsync(TimeSpan.FromMilliseconds(300 * attempt), cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public void Dispose() => _client.Dispose();

        private async Task<byte[]> DownloadOnceAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    linked.CancelAfter(_timeout);
                    using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                    using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false))
                    {
                        var status = (int)response.StatusCode;
                        if (status < 200 || status > 299)
                        {
                            throw new ImageDownloadException(
                                status == 408 || status == 429 || status >= 500 ? ImageFailure.Network : ImageFailure.HttpStatus,
                                "Image request failed with HTTP " + status + ".");
                        }

                        var declared = response.Content.Headers.ContentLength;
                        if (declared.HasValue && declared.Value > _maxBytes)
                        {
                            throw new ImageDownloadException(ImageFailure.TooLarge, "Image is larger than the allowed size.");
                        }

                        var bytes = await ReadLimitedAsync(response.Content, linked.Token).ConfigureAwait(false);
                        if (!ImageSniffer.IsLikelyImage(bytes))
                        {
                            throw new ImageDownloadException(ImageFailure.NotAnImage, "Response is not a recognised image.");
                        }

                        return bytes;
                    }
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ImageDownloadException(ImageFailure.Network, "Image request timed out.");
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is IOException)
            {
                throw new ImageDownloadException(ImageFailure.Network, "Image request failed (" + ex.GetType().Name + ").", ex);
            }
        }

        private async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
        {
            using (var source = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[16 * 1024];
                int read;
                while ((read = await source.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + read > _maxBytes)
                    {
                        throw new ImageDownloadException(ImageFailure.TooLarge, "Image is larger than the allowed size.");
                    }

                    buffer.Write(chunk, 0, read);
                }

                return buffer.ToArray();
            }
        }
    }
}
