using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Http;
using Xmana.Core.Json;

namespace Xmana.Tests
{
    [TestClass]
    public class ApiHttpClientTests
    {
        [TestMethod]
        public async Task ValidResponse_IsParsed()
        {
            var transport = FakeTransport.Always("{\"info\":[{\"nb\":\"1\"}]}");
            var token = await Make.Http(transport).GetJsonAsync("faq", Make.ApiUri);
            Assert.AreEqual(1, JsonItems.Extract(token).Count);
            Assert.AreEqual(1, transport.Count);
        }

        [TestMethod]
        public async Task EmptyArray_IsAValidEmptyResult()
        {
            var token = await Make.Http(FakeTransport.Always("[]")).GetJsonAsync("faq", Make.ApiUri);
            Assert.AreEqual(0, ((JArray)token).Count);
        }

        [TestMethod]
        public async Task MalformedJson_BecomesMalformedError_AndIsNotRetried()
        {
            var transport = FakeTransport.Always("{ not json");
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(ApiErrorKind.Malformed, ex.Kind);
            Assert.AreEqual(1, transport.Count);
        }

        [TestMethod]
        public async Task EmptyBody_BecomesMalformedError()
        {
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(FakeTransport.Always(string.Empty)).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(ApiErrorKind.Malformed, ex.Kind);
        }

        [TestMethod]
        public async Task Http404_FailsImmediately_WithoutRetry()
        {
            var transport = FakeTransport.Always("{}", 404);
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(ApiErrorKind.HttpStatus, ex.Kind);
            Assert.AreEqual(404, ex.StatusCode);
            Assert.IsFalse(ex.IsRetryable);
            Assert.AreEqual(1, transport.Count);
        }

        [TestMethod]
        public async Task Http500_IsRetriedWithExponentialBackoff_ThenSucceeds()
        {
            var delayer = new RecordingDelayer();
            var transport = new FakeTransport((r, n, t) => Task.FromResult(n < 3 ? new TransportResponse(503, "x") : new TransportResponse(200, "[]")));
            var token = await Make.Http(transport, delayer: delayer).GetJsonAsync("faq", Make.ApiUri);

            Assert.AreEqual(0, ((JArray)token).Count);
            Assert.AreEqual(3, transport.Count);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(200) }, delayer.Delays);
        }

        [TestMethod]
        public async Task RetriesAreBounded_NeverForever()
        {
            var transport = FakeTransport.Always("x", 500);
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport, attempts: 3).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(500, ex.StatusCode);
            Assert.IsTrue(ex.IsRetryable);
            Assert.AreEqual(3, transport.Count);
        }

        [TestMethod]
        public async Task Http429And408_AreRetryable_Http4xxOtherwiseNot()
        {
            Assert.IsTrue(RetryPolicy.IsRetryableStatus(429));
            Assert.IsTrue(RetryPolicy.IsRetryableStatus(408));
            Assert.IsFalse(RetryPolicy.IsRetryableStatus(400));
            Assert.IsFalse(RetryPolicy.IsRetryableStatus(401));
            Assert.IsFalse(RetryPolicy.IsRetryableStatus(403));
            Assert.IsFalse(RetryPolicy.IsRetryableStatus(404));
            Assert.IsTrue(RetryPolicy.IsRetryableStatus(502));
            Assert.IsFalse(RetryPolicy.IsRetryableStatus(200));
            await Task.CompletedTask;
        }

        [TestMethod]
        public async Task NetworkFailure_IsRetried_ThenReportedAsNetworkError()
        {
            var transport = new FakeTransport((r, n, t) => throw new HttpRequestException("dns failure"));
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport, attempts: 2).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(ApiErrorKind.Network, ex.Kind);
            Assert.AreEqual(2, transport.Count);
        }

        [TestMethod]
        public async Task NetworkFailure_ThenRecovery_Succeeds()
        {
            var transport = new FakeTransport((r, n, t) => n == 1
                ? throw new System.IO.IOException("reset")
                : Task.FromResult(new TransportResponse(200, "[]")));
            var token = await Make.Http(transport).GetJsonAsync("faq", Make.ApiUri);
            Assert.AreEqual(0, ((JArray)token).Count);
            Assert.AreEqual(2, transport.Count);
        }

        [TestMethod]
        public async Task Timeout_IsReportedAsTimeoutError_AfterRetries()
        {
            var transport = new FakeTransport(async (r, n, t) =>
            {
                await Task.Delay(Timeout.Infinite, t);
                return new TransportResponse(200, "[]");
            });

            var ex = await Assert.ThrowsExceptionAsync<ApiException>(
                () => Make.Http(transport, attempts: 2, timeout: TimeSpan.FromMilliseconds(40)).GetJsonAsync("faq", Make.ApiUri));
            Assert.AreEqual(ApiErrorKind.Timeout, ex.Kind);
            Assert.AreEqual(2, transport.Count);
        }

        [TestMethod]
        public async Task CallerCancellation_SurfacesAsCancellation_NotAsApiError_AndStopsRetrying()
        {
            using (var cts = new CancellationTokenSource())
            {
                var transport = new FakeTransport(async (r, n, t) =>
                {
                    cts.Cancel();
                    await Task.Delay(Timeout.Infinite, t);
                    return new TransportResponse(200, "[]");
                });

                await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => Make.Http(transport).GetJsonAsync("faq", Make.ApiUri, RequestPriority.Primary, cts.Token));
                Assert.AreEqual(1, transport.Count);
            }
        }

        [TestMethod]
        public async Task AlreadyCancelledToken_SendsNothing()
        {
            var transport = FakeTransport.Always("[]");
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => Make.Http(transport).GetJsonAsync("faq", Make.ApiUri, RequestPriority.Primary, cts.Token));
            }

            Assert.AreEqual(0, transport.Count);
        }

        [TestMethod]
        public async Task Post_IsNeverRetried()
        {
            var transport = FakeTransport.Always("x", 503);
            var uri = new Uri(Xmana.Core.Api.CinemanaEndpoints.RatingSummaryUrl);
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport).PostJsonAsync("rating", uri, "{}"));
            Assert.AreEqual(503, ex.StatusCode);
            Assert.AreEqual(1, transport.Count);
            Assert.AreEqual(ApiHttpMethod.Post, transport.Requests[0].Method);
            Assert.AreEqual("{}", transport.Requests[0].Body);
        }

        [DataTestMethod]
        [DataRow("http://cinemana.shabakaty.cc/api/android/faqQuestion")]
        [DataRow("https://evil.example.com/api/android/faqQuestion")]
        [DataRow("https://cinemana.shabakaty.cc.evil.com/x")]
        [DataRow("https://user:pass@cinemana.shabakaty.cc/x")]
        [DataRow("ftp://cinemana.shabakaty.cc/x")]
        public async Task DisallowedDestinations_AreBlocked_BeforeAnyRequest(string url)
        {
            var transport = FakeTransport.Always("[]");
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => Make.Http(transport).GetJsonAsync("x", new Uri(url)));
            Assert.AreEqual(ApiErrorKind.InvalidRequest, ex.Kind);
            Assert.AreEqual(0, transport.Count);
        }

        [TestMethod]
        public async Task ErrorsAndLogs_NeverContainUrls()
        {
            var logger = new ListLogger();
            var transport = FakeTransport.Always("x", 500);
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(
                () => Make.Http(transport, logger).GetJsonAsync("allVideoInfo", new Uri("https://cinemana.shabakaty.cc/api/android/allVideoInfo/id/1?sig=SECRET")));

            Assert.IsFalse(ex.Message.Contains("http"));
            Assert.IsFalse(ex.Message.Contains("SECRET"));
            Assert.IsTrue(logger.Messages.Count > 0);
            foreach (var message in logger.Messages)
            {
                Assert.IsFalse(message.Contains("SECRET"), message);
                Assert.IsFalse(message.Contains("://"), message);
            }
        }

        [TestMethod]
        public async Task Concurrency_IsLimited()
        {
            var active = 0;
            var peak = 0;
            var transport = new FakeTransport(async (r, n, t) =>
            {
                var now = Interlocked.Increment(ref active);
                int seen;
                do
                {
                    seen = Volatile.Read(ref peak);
                }
                while (now > seen && Interlocked.CompareExchange(ref peak, now, seen) != seen);

                await Task.Delay(20);
                Interlocked.Decrement(ref active);
                return new TransportResponse(200, "[]");
            });

            var options = new HttpClientOptions { MaxConcurrentRequests = 2, RetryPolicy = new RetryPolicy(1) };
            var client = new ApiHttpClient(transport, options, null, new RecordingDelayer());
            var tasks = new Task[10];
            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = client.GetJsonAsync("faq", Make.ApiUri);
            }

            await Task.WhenAll(tasks);
            Assert.AreEqual(10, transport.Count);
            Assert.IsTrue(peak <= 2, "peak concurrency was " + peak);
        }
    }

    [TestClass]
    public class RetryPolicyTests
    {
        [TestMethod]
        public void Delay_DoublesPerAttempt_AndIsCapped()
        {
            var policy = new RetryPolicy(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(350));
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1));
            Assert.AreEqual(TimeSpan.FromMilliseconds(200), policy.GetDelay(2));
            Assert.AreEqual(TimeSpan.FromMilliseconds(350), policy.GetDelay(3));
            Assert.AreEqual(TimeSpan.FromMilliseconds(350), policy.GetDelay(50));
        }

        [TestMethod]
        public void InvalidAttemptCount_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => new RetryPolicy(0));
        }
    }
}
