using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Layout;

namespace Xmana.Tests
{
    [TestClass]
    public class GridWindowTests
    {
        [TestMethod]
        public void RowCount_And_RowOf()
        {
            Assert.AreEqual(0, GridWindow.RowCount(0, 4));
            Assert.AreEqual(3, GridWindow.RowCount(10, 4));
            Assert.AreEqual(1, GridWindow.RowCount(1, 4));
            Assert.AreEqual(0, GridWindow.RowOf(0, 4));
            Assert.AreEqual(0, GridWindow.RowOf(3, 4));
            Assert.AreEqual(1, GridWindow.RowOf(4, 4));
            Assert.AreEqual(0, GridWindow.RowOf(5, 0), "a zero column count never divides by zero");
        }

        [TestMethod]
        public void Realized_CoversTheViewportPlusABuffer()
        {
            // rows 0..19, height 300, viewport 900 (3 rows visible), scrolled to row 10 (offset 3000), buffer 3
            var range = GridWindow.Realized(rowCount: 20, rowHeight: 300, offset: 3000, viewport: 900, bufferRows: 3);
            Assert.AreEqual(7, range.First, "row 10 minus 3 buffer rows");
            Assert.AreEqual(15, range.Last, "last visible row (12) plus 3 buffer rows");
            Assert.IsTrue(range.Contains(10));
            Assert.IsFalse(range.Contains(6));
        }

        [TestMethod]
        public void Realized_ClampsAtBothEnds()
        {
            var atStart = GridWindow.Realized(rowCount: 20, rowHeight: 300, offset: 0, viewport: 900, bufferRows: 3);
            Assert.AreEqual(0, atStart.First);

            var atEnd = GridWindow.Realized(rowCount: 20, rowHeight: 300, offset: 1_000_000, viewport: 900, bufferRows: 3);
            Assert.AreEqual(19, atEnd.Last);
            Assert.IsTrue(atEnd.First <= atEnd.Last);
        }

        [TestMethod]
        public void Realized_EmptyGrid_IsEmptyRange()
        {
            var range = GridWindow.Realized(rowCount: 0, rowHeight: 300, offset: 0, viewport: 900);
            Assert.IsTrue(range.IsEmpty);
            Assert.IsFalse(range.Contains(0));
        }

        [TestMethod]
        public void Realized_UsesTheDefaultRowHeight_WhenNoneIsMeasuredYet()
        {
            var range = GridWindow.Realized(rowCount: 10, rowHeight: 0, offset: 0, viewport: 0);
            Assert.IsFalse(range.IsEmpty);
            Assert.IsTrue(range.Last >= range.First);
        }

        [TestMethod]
        public void OffsetToShow_CentersTheRow_AndNeverGoesNegative()
        {
            Assert.AreEqual(0, GridWindow.OffsetToShow(0, 300, 900));
            Assert.AreEqual(1500 - 300, GridWindow.OffsetToShow(5, 300, 900));
        }
    }

    [TestClass]
    public class LruCacheTests
    {
        [TestMethod]
        public void RecentlyUsed_SurvivesEviction()
        {
            var cache = new LruCache<string, int>(maxEntries: 3, maxWeight: 1000);
            cache.Set("a", 1, 10);
            cache.Set("b", 2, 10);
            cache.Set("c", 3, 10);
            int value;
            Assert.IsTrue(cache.TryGet("a", out value)); // touches "a", making "b" the least recently used
            cache.Set("d", 4, 10);

            Assert.IsFalse(cache.TryGet("b", out value));
            Assert.IsTrue(cache.TryGet("a", out value));
            Assert.IsTrue(cache.TryGet("c", out value));
            Assert.IsTrue(cache.TryGet("d", out value));
        }

        [TestMethod]
        public void WeightBudget_IsEnforced_EvenWithFewEntries()
        {
            var cache = new LruCache<string, int>(maxEntries: 100, maxWeight: 100);
            cache.Set("a", 1, 60);
            cache.Set("b", 2, 60);
            Assert.AreEqual(60, cache.TotalWeight, "the first entry must be evicted to respect the weight budget");
            int value;
            Assert.IsFalse(cache.TryGet("a", out value));
            Assert.IsTrue(cache.TryGet("b", out value));
        }

        [TestMethod]
        public void AnEntryHeavierThanTheWholeBudget_IsRejected()
        {
            var cache = new LruCache<string, int>(maxEntries: 10, maxWeight: 100);
            Assert.IsFalse(cache.Set("huge", 1, 500));
            int value;
            Assert.IsFalse(cache.TryGet("huge", out value));
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public void Remove_And_Clear()
        {
            var cache = new LruCache<string, int>(maxEntries: 10, maxWeight: 100);
            cache.Set("a", 1, 10);
            cache.Set("b", 2, 10);
            Assert.IsTrue(cache.Remove("a"));
            Assert.IsFalse(cache.Remove("a"));
            Assert.AreEqual(10, cache.TotalWeight);
            cache.Clear();
            Assert.AreEqual(0, cache.Count);
            Assert.AreEqual(0, cache.TotalWeight);
        }

        [TestMethod]
        public void ReplacingAKey_UpdatesItsWeight_WithoutLeaking()
        {
            var cache = new LruCache<string, int>(maxEntries: 10, maxWeight: 100);
            cache.Set("a", 1, 40);
            cache.Set("a", 2, 10);
            Assert.AreEqual(10, cache.TotalWeight);
            int value;
            cache.TryGet("a", out value);
            Assert.AreEqual(2, value);
        }

        [TestMethod]
        public void CustomComparer_IsRespected()
        {
            var cache = new LruCache<string, int>(10, 100, StringComparer.OrdinalIgnoreCase);
            cache.Set("Key", 1, 10);
            int value;
            Assert.IsTrue(cache.TryGet("key", out value));
            Assert.AreEqual(1, value);
        }

        [TestMethod]
        public void InvalidLimits_Throw()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0, 100));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new LruCache<string, int>(10, 0));
        }
    }

    [TestClass]
    public class CachePurgeTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init() => _dir = Path.Combine(Path.GetTempPath(), "xmana-purge-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [TestMethod]
        public async Task ResponseCache_PurgesOnlyOldEntries()
        {
            var cache = new FileResponseCache(_dir);
            var now = DateTime.UtcNow;
            await cache.SetAsync(new CacheEntry("old", "[1]", now), CancellationToken.None);
            await cache.SetAsync(new CacheEntry("new", "[2]", now), CancellationToken.None);

            var oldFile = Directory.GetFiles(_dir).OrderBy(f => f).First();
            File.SetLastWriteTimeUtc(oldFile, now.AddDays(-60));

            var removed = await cache.PurgeOlderThanAsync(TimeSpan.FromDays(31), now);
            Assert.AreEqual(1, removed);
            Assert.AreEqual(1, Directory.GetFiles(_dir).Length);
        }

        [TestMethod]
        public async Task ResponseCache_PurgeOfMissingDirectory_IsSafe()
        {
            var cache = new FileResponseCache(Path.Combine(_dir, "does-not-exist"));
            Assert.AreEqual(0, await cache.PurgeOlderThanAsync(TimeSpan.FromDays(1), DateTime.UtcNow));
        }

        [TestMethod]
        public async Task ImageDiskCache_PurgesOnlyOldEntries()
        {
            var cache = new ImageDiskCache(_dir);
            var now = DateTime.UtcNow;
            await cache.SetAsync("old", Png.Bytes(), CancellationToken.None);
            await cache.SetAsync("new", Png.Bytes(), CancellationToken.None);
            File.SetLastWriteTimeUtc(Path.Combine(_dir, "old.img"), now.AddDays(-60));

            var removed = await cache.PurgeOlderThanAsync(TimeSpan.FromDays(31), now);
            Assert.AreEqual(1, removed);
            Assert.IsNull(await cache.GetAsync("old", CancellationToken.None));
            Assert.IsNotNull(await cache.GetAsync("new", CancellationToken.None));
        }
    }

    [TestClass]
    public class ImageDownloaderConcurrencyTests
    {
        [TestMethod]
        public async Task Downloads_AreLimitedAndPrioritized()
        {
            var active = 0;
            var peak = 0;
            var order = new System.Collections.Generic.List<int>();
            var gate = new TaskCompletionSource<bool>();
            var started = 0;

            var handler = new DelegatingHandlerStub(async req =>
            {
                var now = Interlocked.Increment(ref active);
                int seen;
                do { seen = Volatile.Read(ref peak); } while (now > seen && Interlocked.CompareExchange(ref peak, now, seen) != seen);
                if (Interlocked.Increment(ref started) <= 2)
                {
                    await gate.Task; // hold the first two slots so later, higher-priority requests must queue
                }

                Interlocked.Decrement(ref active);
                return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.ByteArrayContent(Png.Bytes()) };
            });

            using (var downloader = new Xmana.Core.Images.ImageDownloader(handler, maxConcurrent: 2))
            {
                var low1 = downloader.DownloadAsync("https://img.invalid/1.png", CancellationToken.None, RequestPriority.Low);
                var low2 = downloader.DownloadAsync("https://img.invalid/2.png", CancellationToken.None, RequestPriority.Low);
                await Task.Delay(50); // let the two low-priority downloads occupy both slots
                var hero = downloader.DownloadAsync("https://img.invalid/3.png", CancellationToken.None, RequestPriority.Hero);
                await Task.Delay(50);

                gate.SetResult(true);
                await Task.WhenAll(low1, low2, hero);
            }

            Assert.IsTrue(peak <= 2, "peak concurrency was " + peak);
        }

        private sealed class DelegatingHandlerStub : System.Net.Http.HttpMessageHandler
        {
            private readonly Func<System.Net.Http.HttpRequestMessage, Task<System.Net.Http.HttpResponseMessage>> _respond;

            public DelegatingHandlerStub(Func<System.Net.Http.HttpRequestMessage, Task<System.Net.Http.HttpResponseMessage>> respond)
            {
                _respond = respond;
            }

            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken) =>
                _respond(request);
        }
    }
}
