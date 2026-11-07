using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Images;
using Xmana.Core.Logging;

namespace Xmana.App.Services
{
    /// <summary>
    /// Image service (PROMPT.md section 36): memory cache (LRU of decoded bitmaps), disk cache (hash-named files, never
    /// URLs), network download with size/type checks, in-flight de-duplication, cancellation, a short negative cache so
    /// broken images are not retried on every scroll, and corrupt-image handling. Decoding runs inside
    /// <c>BitmapImage.SetSourceAsync</c> at a capped pixel width, never at full resolution on the UI thread.
    /// Call from the UI thread.
    /// </summary>
    internal sealed class ImageLoader
    {
        /// <summary>Memory budget for decoded bitmaps (PROMPT.md section 57): a hard ceiling regardless of how many large posters are seen.</summary>
        public const long MaxMemoryBytes = 48L * 1024 * 1024;

        private const int MaxMemoryEntries = 160;
        private static readonly TimeSpan FailureMemory = TimeSpan.FromSeconds(45);

        private readonly ImageDownloader _downloader;
        private readonly ImageDiskCache _disk;
        private readonly IXmanaLogger _logger;
        private readonly object _gate = new object();
        private readonly LruCache<string, BitmapImage> _memory = new LruCache<string, BitmapImage>(MaxMemoryEntries, MaxMemoryBytes, StringComparer.Ordinal);
        private readonly Dictionary<string, Task<byte[]>> _inFlight = new Dictionary<string, Task<byte[]>>(StringComparer.Ordinal);
        private readonly Dictionary<string, DateTime> _failed = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        public ImageLoader(ImageDownloader downloader, ImageDiskCache disk, IXmanaLogger logger)
        {
            _downloader = downloader;
            _disk = disk;
            _logger = logger;
        }

        /// <summary>Returns a decoded bitmap, or null when the image is unavailable (callers keep their placeholder).</summary>
        public Task<BitmapImage> LoadAsync(string url, int decodePixelWidth, CancellationToken cancellationToken) =>
            LoadAsync(url, decodePixelWidth, cancellationToken, RequestPriority.Low);

        /// <summary>
        /// As above, with an explicit priority (PROMPT.md section 57): the hero banner and currently visible cards should win scarce
        /// download slots over posters the viewer has not scrolled to yet.
        /// </summary>
        public async Task<BitmapImage> LoadAsync(string url, int decodePixelWidth, CancellationToken cancellationToken, RequestPriority priority)
        {
            var diskKey = ImageDiskCache.KeyFor(url);
            var memoryKey = diskKey + "@" + decodePixelWidth;

            var cached = GetFromMemory(memoryKey);
            if (cached != null)
            {
                return cached;
            }

            if (RecentlyFailed(diskKey))
            {
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = await WithCancellation(GetBytesAsync(url, diskKey, priority), cancellationToken);
            }
            catch (ImageDownloadException ex)
            {
                MarkFailed(diskKey);
                _logger.Debug("image unavailable (" + ex.Failure + ")");
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var bitmap = await DecodeAsync(bytes, decodePixelWidth);
            if (bitmap == null)
            {
                // Bytes looked like an image but could not be decoded: drop them so they are fetched again later.
                await _disk.RemoveAsync(diskKey);
                MarkFailed(diskKey);
                return null;
            }

            // Weighted by decoded size (4 bytes/pixel, BGRA32), not the compressed file size, so the memory budget is real.
            AddToMemory(memoryKey, bitmap, (long)bitmap.PixelWidth * bitmap.PixelHeight * 4);
            return bitmap;
        }

        /// <summary>Warms the disk cache without decoding (prefetch for rails about to scroll into view).</summary>
        public Task PrefetchAsync(string url)
        {
            var diskKey = ImageDiskCache.KeyFor(url);
            if (RecentlyFailed(diskKey))
            {
                return Task.CompletedTask;
            }

            return GetBytesAsync(url, diskKey, RequestPriority.Low).ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.ExecuteSynchronously);
        }

        public void ClearMemory()
        {
            lock (_gate)
            {
                _memory.Clear();
            }
        }

        private Task<byte[]> GetBytesAsync(string url, string diskKey, RequestPriority priority)
        {
            lock (_gate)
            {
                Task<byte[]> existing;
                if (_inFlight.TryGetValue(diskKey, out existing))
                {
                    return existing;
                }

                var task = Task.Run(async () =>
                {
                    var onDisk = await _disk.GetAsync(diskKey, CancellationToken.None).ConfigureAwait(false);
                    if (onDisk != null)
                    {
                        return onDisk;
                    }

                    var downloaded = await _downloader.DownloadAsync(url, CancellationToken.None, priority).ConfigureAwait(false);
                    await _disk.SetAsync(diskKey, downloaded, CancellationToken.None).ConfigureAwait(false);
                    return downloaded;
                });

                _inFlight[diskKey] = task;
                task.ContinueWith(
                    t =>
                    {
                        lock (_gate)
                        {
                            _inFlight.Remove(diskKey);
                        }
                    },
                    TaskContinuationOptions.ExecuteSynchronously);
                return task;
            }
        }

        private static async Task<T> WithCancellation<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                return await task;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
            {
                var finished = await Task.WhenAny(task, cancelled.Task);
                if (finished != task)
                {
                    // The shared download keeps running for other waiters; only this caller stops waiting.
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task;
        }

        private static async Task<BitmapImage> DecodeAsync(byte[] bytes, int decodePixelWidth)
        {
            try
            {
                var bitmap = new BitmapImage { DecodePixelWidth = decodePixelWidth, DecodePixelType = DecodePixelType.Physical };
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);
                    await bitmap.SetSourceAsync(stream);
                }

                return bitmap;
            }
            catch (Exception)
            {
                return null; // corrupt or unsupported image
            }
        }

        private BitmapImage GetFromMemory(string key)
        {
            lock (_gate)
            {
                BitmapImage bitmap;
                return _memory.TryGet(key, out bitmap) ? bitmap : null;
            }
        }

        private void AddToMemory(string key, BitmapImage bitmap, long weight)
        {
            lock (_gate)
            {
                _memory.Set(key, bitmap, weight);
            }
        }

        private bool RecentlyFailed(string diskKey)
        {
            lock (_gate)
            {
                DateTime when;
                if (_failed.TryGetValue(diskKey, out when))
                {
                    if (DateTime.UtcNow - when < FailureMemory)
                    {
                        return true;
                    }

                    _failed.Remove(diskKey);
                }

                return false;
            }
        }

        private void MarkFailed(string diskKey)
        {
            lock (_gate)
            {
                _failed[diskKey] = DateTime.UtcNow;
            }
        }

    }
}
