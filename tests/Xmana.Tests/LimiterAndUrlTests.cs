using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Http;
using Xmana.Core.Logging;

namespace Xmana.Tests
{
    [TestClass]
    public class PriorityConcurrencyLimiterTests
    {
        [TestMethod]
        public async Task FreeSlot_IsGrantedImmediately_AndReleasedOnDispose()
        {
            var limiter = new PriorityConcurrencyLimiter(1);
            var lease = await limiter.AcquireAsync(RequestPriority.Low, CancellationToken.None);
            Assert.AreEqual(1, limiter.ActiveCount);
            lease.Dispose();
            lease.Dispose(); // double dispose must not free a second slot
            Assert.AreEqual(0, limiter.ActiveCount);
        }

        [TestMethod]
        public async Task WaitingRequests_AreServedByPriority_ThenFifo()
        {
            var limiter = new PriorityConcurrencyLimiter(1);
            var first = await limiter.AcquireAsync(RequestPriority.Primary, CancellationToken.None);

            var order = new List<string>();
            Task Waiter(string name, RequestPriority priority) => Task.Run(async () =>
            {
                using (await limiter.AcquireAsync(priority, CancellationToken.None))
                {
                    lock (order)
                    {
                        order.Add(name);
                    }
                }
            });

            var low = Waiter("low", RequestPriority.Low);
            await WaitUntil(() => limiter.WaitingCount == 1);
            var secondary1 = Waiter("secondary1", RequestPriority.Secondary);
            await WaitUntil(() => limiter.WaitingCount == 2);
            var secondary2 = Waiter("secondary2", RequestPriority.Secondary);
            await WaitUntil(() => limiter.WaitingCount == 3);
            var hero = Waiter("hero", RequestPriority.Hero);
            await WaitUntil(() => limiter.WaitingCount == 4);

            first.Dispose();
            await Task.WhenAll(low, secondary1, secondary2, hero);

            CollectionAssert.AreEqual(new[] { "hero", "secondary1", "secondary2", "low" }, order);
            Assert.AreEqual(0, limiter.ActiveCount);
        }

        [TestMethod]
        public async Task CancelledWaiter_LeavesTheQueue_AndFreesNothingItNeverHeld()
        {
            var limiter = new PriorityConcurrencyLimiter(1);
            var holder = await limiter.AcquireAsync(RequestPriority.Primary, CancellationToken.None);

            using (var cts = new CancellationTokenSource())
            {
                var waiting = limiter.AcquireAsync(RequestPriority.Primary, cts.Token);
                await WaitUntil(() => limiter.WaitingCount == 1);
                cts.Cancel();
                await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => waiting);
                Assert.AreEqual(0, limiter.WaitingCount);
            }

            Assert.AreEqual(1, limiter.ActiveCount);
            holder.Dispose();
            Assert.AreEqual(0, limiter.ActiveCount);
        }

        [TestMethod]
        public async Task AlreadyCancelledToken_ThrowsWithoutQueueing()
        {
            var limiter = new PriorityConcurrencyLimiter(1);
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => limiter.AcquireAsync(RequestPriority.Hero, cts.Token));
            }

            Assert.AreEqual(0, limiter.ActiveCount);
            Assert.AreEqual(0, limiter.WaitingCount);
        }

        [TestMethod]
        public void InvalidLimit_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new PriorityConcurrencyLimiter(0));
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (var i = 0; i < 400 && !condition(); i++)
            {
                await Task.Delay(5);
            }

            Assert.IsTrue(condition(), "condition was not reached in time");
        }
    }

    [TestClass]
    public class UrlValidatorTests
    {
        [DataTestMethod]
        [DataRow("https://cinemana.shabakaty.cc/api/android/x", true)]
        [DataRow("HTTPS://CINEMANA.SHABAKATY.CC/api/android/x", true)]
        [DataRow("https://recommend.shabakaty.cc/api/recommendation/recommend", true)]
        [DataRow("https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types", true)]
        [DataRow("http://cinemana.shabakaty.cc/x", false)]
        [DataRow("https://shabakaty.cc/x", false)]
        [DataRow("https://cinemana.shabakaty.cc.evil.com/x", false)]
        [DataRow("https://evil.com/cinemana.shabakaty.cc", false)]
        [DataRow("https://u:p@cinemana.shabakaty.cc/x", false)]
        public void ApiHosts_AreAllowListedAndHttpsOnly(string url, bool allowed)
        {
            Assert.AreEqual(allowed, UrlValidator.IsAllowedApiUri(new Uri(url)));
        }

        [TestMethod]
        public void NullAndRelativeUris_AreRejected()
        {
            Assert.IsFalse(UrlValidator.IsAllowedApiUri(null));
            Assert.IsFalse(UrlValidator.IsAllowedApiUri(new Uri("/relative", UriKind.Relative)));
        }

        [DataTestMethod]
        [DataRow("https://img.example.com/a.jpg", true)]
        [DataRow("  https://img.example.com/a.jpg  ", true)]
        [DataRow("http://img.example.com/a.jpg", false)]
        [DataRow("javascript:alert(1)", false)]
        [DataRow("file:///c:/x", false)]
        [DataRow("//img.example.com/a.jpg", false)]
        [DataRow("not a url", false)]
        [DataRow("", false)]
        [DataRow("https://user:pw@img.example.com/a.jpg", false)]
        public void ServerSuppliedUrls_MustBeAbsoluteHttpsWithoutCredentials(string value, bool ok)
        {
            Uri? uri;
            Assert.AreEqual(ok, UrlValidator.TryGetHttpsUri(value, out uri));
        }
    }

    [TestClass]
    public class LogRedactorTests
    {
        [TestMethod]
        public void Urls_AreRemovedFromMessages()
        {
            var text = LogRedactor.Redact("Playback stream received from https://cdn.example.com/v.mp4?sig=abc&exp=1 quality 720p");
            Assert.AreEqual("Playback stream received from [url] quality 720p", text);
        }

        [TestMethod]
        public void MultipleUrlsAndSchemes_AreAllRemoved()
        {
            var text = LogRedactor.Redact("a http://x.test/1 b https://y.test/2?k=v c ftp://z.test/3");
            Assert.AreEqual("a [url] b [url] c [url]", text);
        }

        [TestMethod]
        public void NullEmptyAndPlainText_AreSafe()
        {
            Assert.AreEqual(string.Empty, LogRedactor.Redact(null));
            Assert.AreEqual(string.Empty, LogRedactor.Redact(string.Empty));
            Assert.AreEqual("Quality: 720p, Video ID: 1119122", LogRedactor.Redact("Quality: 720p, Video ID: 1119122"));
        }

        [TestMethod]
        public void LoggerExtensions_AlwaysRedact()
        {
            var logger = new ListLogger();
            logger.Info("got https://signed.example.com/a?sig=SECRET");
            logger.Warn("x http://signed.example.com/b");
            logger.Error("y https://signed.example.com/c");
            logger.Debug("z https://signed.example.com/d");
            Assert.AreEqual(4, logger.Messages.Count);
            foreach (var message in logger.Messages)
            {
                Assert.IsFalse(message.Contains("signed.example.com"));
                Assert.IsFalse(message.Contains("SECRET"));
            }
        }
    }
}
