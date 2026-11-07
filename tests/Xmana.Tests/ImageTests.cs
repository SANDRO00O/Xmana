using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Images;
using Xmana.Core.Json;

namespace Xmana.Tests
{
    internal static class Png
    {
        public static byte[] Bytes(int extra = 0)
        {
            var bytes = new byte[32 + extra];
            bytes[0] = 0x89;
            bytes[1] = 0x50;
            bytes[2] = 0x4E;
            bytes[3] = 0x47;
            return bytes;
        }
    }

    [TestClass]
    public class ImageSelectorTests
    {
        private static Xmana.Core.Models.VideoSummary Video(string json) => VideoMapper.ToSummary(JsonParser.Parse(json))!;

        [TestMethod]
        public void MediumThumbnail_PrefersMediumThenThumbThenFull()
        {
            var v = Video("{\"nb\":\"1\",\"img\":\"https://i.invalid/full.jpg\",\"imgThumb\":\"https://i.invalid/t.jpg\",\"imgMediumThumb\":\"https://i.invalid/m.jpg\"}");
            Assert.AreEqual("https://i.invalid/m.jpg", ImageSelector.For(v, ImageRole.MediumThumbnail));
            Assert.AreEqual("https://i.invalid/t.jpg", ImageSelector.For(v, ImageRole.Thumbnail));
            Assert.AreEqual("https://i.invalid/full.jpg", ImageSelector.For(v, ImageRole.Poster));
            Assert.AreEqual("https://i.invalid/full.jpg", ImageSelector.For(v, ImageRole.Banner));
        }

        [TestMethod]
        public void FallsBack_WhenPreferredFieldIsMissing()
        {
            var v = Video("{\"nb\":\"1\",\"img\":\"https://i.invalid/full.jpg\"}");
            Assert.AreEqual("https://i.invalid/full.jpg", ImageSelector.For(v, ImageRole.MediumThumbnail));
            Assert.AreEqual("https://i.invalid/full.jpg", ImageSelector.For(v, ImageRole.Thumbnail));
        }

        [TestMethod]
        public void SignedObjUrlFields_AreOnlyAFallback()
        {
            var both = Video("{\"nb\":\"1\",\"imgMediumThumb\":\"https://i.invalid/m.jpg\",\"imgMediumThumbObjUrl\":\"https://signed.invalid/m?sig=1\"}");
            Assert.AreEqual("https://i.invalid/m.jpg", ImageSelector.For(both, ImageRole.MediumThumbnail));

            var onlySigned = Video("{\"nb\":\"1\",\"imgMediumThumbObjUrl\":\"https://signed.invalid/m?sig=1\"}");
            Assert.AreEqual("https://signed.invalid/m?sig=1", ImageSelector.For(onlySigned, ImageRole.MediumThumbnail));
        }

        [TestMethod]
        public void NonHttpsAndBlankValues_AreRejected()
        {
            Assert.IsNull(ImageSelector.For(Video("{\"nb\":\"1\"}"), ImageRole.Poster));
            Assert.IsNull(ImageSelector.For(Video("{\"nb\":\"1\",\"img\":\"http://i.invalid/a.jpg\",\"imgThumb\":\"  \",\"imgMediumThumb\":\"javascript:1\"}"), ImageRole.Poster));
        }

        [TestMethod]
        public void StaffImages_PreferMediumThumb()
        {
            var staff = VideoMapper.ToStaff(JsonParser.Parse("{\"nb\":\"2766\",\"name\":\"X\",\"staff_img\":\"https://i.invalid/s.jpg\",\"staff_img_medium_thumb\":\"https://i.invalid/sm.jpg\"}"))!;
            Assert.AreEqual("https://i.invalid/sm.jpg", ImageSelector.For(staff));
            Assert.IsNull(ImageSelector.For(VideoMapper.ToStaff(JsonParser.Parse("{\"name\":\"Y\"}"))!));
        }
    }

    [TestClass]
    public class ImageSnifferTests
    {
        [TestMethod]
        public void RecognisesCommonFormats()
        {
            var jpeg = new byte[20]; jpeg[0] = 0xFF; jpeg[1] = 0xD8; jpeg[2] = 0xFF;
            var gif = new byte[20]; gif[0] = (byte)'G'; gif[1] = (byte)'I'; gif[2] = (byte)'F'; gif[3] = (byte)'8';
            var webp = new byte[20]; webp[0] = (byte)'R'; webp[1] = (byte)'I'; webp[2] = (byte)'F'; webp[3] = (byte)'F'; webp[8] = (byte)'W'; webp[9] = (byte)'E'; webp[10] = (byte)'B'; webp[11] = (byte)'P';
            Assert.IsTrue(ImageSniffer.IsLikelyImage(Png.Bytes()));
            Assert.IsTrue(ImageSniffer.IsLikelyImage(jpeg));
            Assert.IsTrue(ImageSniffer.IsLikelyImage(gif));
            Assert.IsTrue(ImageSniffer.IsLikelyImage(webp));
        }

        [TestMethod]
        public void RejectsHtmlJsonEmptyAndTruncatedBytes()
        {
            Assert.IsFalse(ImageSniffer.IsLikelyImage(System.Text.Encoding.UTF8.GetBytes("<html><body>error</body></html>")));
            Assert.IsFalse(ImageSniffer.IsLikelyImage(System.Text.Encoding.UTF8.GetBytes("{\"error\":\"nope\",\"code\":500}")));
            Assert.IsFalse(ImageSniffer.IsLikelyImage(new byte[0]));
            Assert.IsFalse(ImageSniffer.IsLikelyImage(null));
            Assert.IsFalse(ImageSniffer.IsLikelyImage(new byte[] { 0x89, 0x50 }));
        }
    }

    [TestClass]
    public class ImageDownloaderTests
    {
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<int, HttpResponseMessage> _respond;
            private int _count;

            public Handler(Func<int, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public int Count => _count;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var n = Interlocked.Increment(ref _count);
                return Task.FromResult(_respond(n));
            }
        }

        private static HttpResponseMessage Ok(byte[] body) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

        private static ImageDownloader Downloader(Handler handler, long max = 1024 * 1024, int attempts = 2) =>
            new ImageDownloader(handler, max, TimeSpan.FromSeconds(5), attempts, new RecordingDelayer());

        [TestMethod]
        public async Task ValidImage_IsReturned()
        {
            var handler = new Handler(n => Ok(Png.Bytes(100)));
            using (var d = Downloader(handler))
            {
                var bytes = await d.DownloadAsync("https://img.invalid/a.png", CancellationToken.None);
                Assert.AreEqual(132, bytes.Length);
            }
        }

        [DataTestMethod]
        [DataRow("http://img.invalid/a.png")]
        [DataRow("ftp://img.invalid/a.png")]
        [DataRow("not a url")]
        [DataRow("")]
        public async Task NonHttpsUrls_AreRefusedWithoutARequest(string url)
        {
            var handler = new Handler(n => Ok(Png.Bytes()));
            using (var d = Downloader(handler))
            {
                var ex = await Assert.ThrowsExceptionAsync<ImageDownloadException>(() => d.DownloadAsync(url, CancellationToken.None));
                Assert.AreEqual(ImageFailure.InvalidUrl, ex.Failure);
            }

            Assert.AreEqual(0, handler.Count);
        }

        [TestMethod]
        public async Task ServerErrors_AreRetriedOnce_ThenSucceed()
        {
            var handler = new Handler(n => n == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Ok(Png.Bytes()));
            using (var d = Downloader(handler))
            {
                await d.DownloadAsync("https://img.invalid/a.png", CancellationToken.None);
            }

            Assert.AreEqual(2, handler.Count);
        }

        [TestMethod]
        public async Task NotFound_IsNotRetried()
        {
            var handler = new Handler(n => new HttpResponseMessage(HttpStatusCode.NotFound));
            using (var d = Downloader(handler))
            {
                var ex = await Assert.ThrowsExceptionAsync<ImageDownloadException>(() => d.DownloadAsync("https://img.invalid/a.png", CancellationToken.None));
                Assert.AreEqual(ImageFailure.HttpStatus, ex.Failure);
                Assert.IsFalse(ex.IsRetryable);
            }

            Assert.AreEqual(1, handler.Count);
        }

        [TestMethod]
        public async Task HtmlErrorPageWithStatus200_IsRejectedAsNotAnImage()
        {
            var handler = new Handler(n => Ok(System.Text.Encoding.UTF8.GetBytes("<html>blocked by firewall, please sign in</html>")));
            using (var d = Downloader(handler))
            {
                var ex = await Assert.ThrowsExceptionAsync<ImageDownloadException>(() => d.DownloadAsync("https://img.invalid/a.png", CancellationToken.None));
                Assert.AreEqual(ImageFailure.NotAnImage, ex.Failure);
            }

            Assert.AreEqual(1, handler.Count);
        }

        [TestMethod]
        public async Task OversizedImages_AreRejected_ByDeclaredAndByActualSize()
        {
            var handler = new Handler(n => Ok(Png.Bytes(5000)));
            using (var d = Downloader(handler, max: 1000))
            {
                var ex = await Assert.ThrowsExceptionAsync<ImageDownloadException>(() => d.DownloadAsync("https://img.invalid/a.png", CancellationToken.None));
                Assert.AreEqual(ImageFailure.TooLarge, ex.Failure);
            }
        }

        [TestMethod]
        public async Task Cancellation_IsNotWrapped()
        {
            var handler = new Handler(n => Ok(Png.Bytes()));
            using (var d = Downloader(handler))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                OperationCanceledException? caught = null;
                try
                {
                    await d.DownloadAsync("https://img.invalid/a.png", cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(caught);
            }
        }

        [TestMethod]
        public async Task ErrorMessages_NeverContainTheUrl()
        {
            var handler = new Handler(n => new HttpResponseMessage(HttpStatusCode.NotFound));
            using (var d = Downloader(handler))
            {
                var ex = await Assert.ThrowsExceptionAsync<ImageDownloadException>(() => d.DownloadAsync("https://img.invalid/a.png?sig=SECRET", CancellationToken.None));
                Assert.IsFalse(ex.Message.Contains("SECRET"));
                Assert.IsFalse(ex.Message.Contains("img.invalid"));
            }
        }
    }

    [TestClass]
    public class ImageDiskCacheTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init() => _dir = Path.Combine(Path.GetTempPath(), "xmana-img-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [TestMethod]
        public void Keys_AreStableHashes_ThatNeverContainTheUrl()
        {
            var a = ImageDiskCache.KeyFor("https://img.invalid/a.png?sig=SECRET");
            Assert.AreEqual(a, ImageDiskCache.KeyFor("https://img.invalid/a.png?sig=SECRET"));
            Assert.AreNotEqual(a, ImageDiskCache.KeyFor("https://img.invalid/b.png"));
            Assert.AreEqual(64, a.Length);
            Assert.IsFalse(a.Contains("SECRET"));
        }

        [TestMethod]
        public async Task RoundTrip_AndNothingUrlLikeOnDisk()
        {
            var cache = new ImageDiskCache(_dir);
            var key = ImageDiskCache.KeyFor("https://img.invalid/a.png?sig=SECRET");
            await cache.SetAsync(key, Png.Bytes(50), CancellationToken.None);

            var read = await cache.GetAsync(key, CancellationToken.None);
            Assert.AreEqual(82, read!.Length);
            foreach (var file in Directory.GetFiles(_dir))
            {
                Assert.IsFalse(Path.GetFileName(file).Contains("SECRET"));
            }
        }

        [TestMethod]
        public async Task NonImageBytes_AreNeverStored_AndCorruptFilesAreDeleted()
        {
            var cache = new ImageDiskCache(_dir);
            await cache.SetAsync("k1", System.Text.Encoding.UTF8.GetBytes("<html>nope nope nope</html>"), CancellationToken.None);
            Assert.IsNull(await cache.GetAsync("k1", CancellationToken.None));

            await cache.SetAsync("k2", Png.Bytes(), CancellationToken.None);
            var file = Directory.GetFiles(_dir).Single();
            File.WriteAllText(file, "garbage that is long enough");
            Assert.IsNull(await cache.GetAsync("k2", CancellationToken.None));
            Assert.IsFalse(File.Exists(file));
        }

        [TestMethod]
        public async Task Miss_Remove_Clear()
        {
            var cache = new ImageDiskCache(_dir);
            Assert.IsNull(await cache.GetAsync("missing", CancellationToken.None));
            await cache.SetAsync("a", Png.Bytes(), CancellationToken.None);
            await cache.SetAsync("b", Png.Bytes(), CancellationToken.None);
            await cache.RemoveAsync("a");
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));
            await cache.ClearAsync();
            Assert.IsNull(await cache.GetAsync("b", CancellationToken.None));
        }

        [TestMethod]
        public async Task OverBudget_PrunesOldestFirst()
        {
            var cache = new ImageDiskCache(_dir, 3000);
            for (var i = 0; i < 50; i++)
            {
                await cache.SetAsync("k" + i, Png.Bytes(500), CancellationToken.None);
                await Task.Delay(2);
            }

            var total = new DirectoryInfo(_dir).GetFiles("*.img").Sum(f => f.Length);
            Assert.IsTrue(total <= 3000 + 25 * 540, "directory should stay near its budget but was " + total);
            Assert.IsNotNull(await cache.GetAsync("k49", CancellationToken.None));
        }
    }
}
