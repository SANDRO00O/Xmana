using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Http;

namespace Xmana.Tests
{
    [TestClass]
    public class HttpClientTransportTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
            {
                _respond = respond;
            }

            public HttpRequestMessage? Last { get; private set; }

            public string? LastBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Last = request;
                LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                return _respond(request);
            }
        }

        private static HttpResponseMessage Response(byte[] body, string contentType, HttpStatusCode status = HttpStatusCode.OK)
        {
            var response = new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
            response.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
            return response;
        }

        [TestMethod]
        public async Task Get_SendsJsonAcceptHeader_AndReturnsStatusAndBody()
        {
            var handler = new StubHandler(r => Response(Encoding.UTF8.GetBytes("[1]"), "application/json"));
            using (var transport = new HttpClientTransport(handler))
            {
                var result = await transport.SendAsync(new TransportRequest(ApiHttpMethod.Get, Make.ApiUri), CancellationToken.None);
                Assert.AreEqual(200, result.StatusCode);
                Assert.AreEqual("[1]", result.Body);
                Assert.AreEqual(HttpMethod.Get, handler.Last!.Method);
                Assert.IsTrue(handler.Last.Headers.Accept.ToString().Contains("application/json"));
            }
        }

        [TestMethod]
        public async Task Post_SendsTheJsonBodyWithContentType()
        {
            var handler = new StubHandler(r => Response(Encoding.UTF8.GetBytes("{}"), "application/json"));
            using (var transport = new HttpClientTransport(handler))
            {
                await transport.SendAsync(new TransportRequest(ApiHttpMethod.Post, Make.ApiUri, "{\"a\":1}", "application/json"), CancellationToken.None);
                Assert.AreEqual(HttpMethod.Post, handler.Last!.Method);
                Assert.AreEqual("{\"a\":1}", handler.LastBody);
                Assert.AreEqual("application/json", handler.Last.Content!.Headers.ContentType!.MediaType);
            }
        }

        [TestMethod]
        public async Task ErrorStatuses_AreReturnedNotThrown()
        {
            var handler = new StubHandler(r => Response(Encoding.UTF8.GetBytes("nope"), "text/plain", HttpStatusCode.NotFound));
            using (var transport = new HttpClientTransport(handler))
            {
                var result = await transport.SendAsync(new TransportRequest(ApiHttpMethod.Get, Make.ApiUri), CancellationToken.None);
                Assert.AreEqual(404, result.StatusCode);
                Assert.AreEqual("nope", result.Body);
            }
        }

        [TestMethod]
        public void Decoding_Utf8_Arabic_AndByteOrderMark()
        {
            var arabic = "المحقق كونان";
            Assert.AreEqual(arabic, HttpClientTransport.DecodeBody(Encoding.UTF8.GetBytes(arabic), null));
            Assert.AreEqual(arabic, HttpClientTransport.DecodeBody(Encoding.UTF8.GetBytes(arabic), "utf-8"));
            Assert.AreEqual(arabic, HttpClientTransport.DecodeBody(Encoding.UTF8.GetBytes(arabic), "\"UTF-8\""));

            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat2(Encoding.UTF8.GetBytes("[1]"));
            Assert.AreEqual("[1]", HttpClientTransport.DecodeBody(withBom, null));
        }

        [TestMethod]
        public void Decoding_UnknownCharset_FallsBackToUtf8_AndEmptyIsEmpty()
        {
            Assert.AreEqual("héllo", HttpClientTransport.DecodeBody(Encoding.UTF8.GetBytes("héllo"), "x-does-not-exist"));
            Assert.AreEqual(string.Empty, HttpClientTransport.DecodeBody(new byte[0], "utf-8"));
            Assert.AreEqual(string.Empty, HttpClientTransport.DecodeBody(null!, null));
        }

        [TestMethod]
        public void Decoding_DeclaredLatin1_IsHonoured()
        {
            var bytes = Encoding.GetEncoding("iso-8859-1").GetBytes("caf\u00E9");
            Assert.AreEqual("caf\u00E9", HttpClientTransport.DecodeBody(bytes, "iso-8859-1"));
        }

        [TestMethod]
        public async Task TransportFailure_Throws_AndCancellationIsHonoured()
        {
            var failing = new StubHandler(r => throw new HttpRequestException("dns"));
            using (var transport = new HttpClientTransport(failing))
            {
                await Assert.ThrowsExceptionAsync<HttpRequestException>(() => transport.SendAsync(new TransportRequest(ApiHttpMethod.Get, Make.ApiUri), CancellationToken.None));
            }

            var ok = new StubHandler(r => Response(new byte[0], "application/json"));
            using (var transport = new HttpClientTransport(ok))
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                OperationCanceledException? caught = null;
                try
                {
                    await transport.SendAsync(new TransportRequest(ApiHttpMethod.Get, Make.ApiUri), cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    caught = ex; // TaskCanceledException or OperationCanceledException depending on the runtime
                }

                Assert.IsNotNull(caught, "a cancelled token must cancel the request");
            }
        }
    }

    internal static class ByteArrayExtensions
    {
        public static byte[] Concat2(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, result, 0, first.Length);
            Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
            return result;
        }
    }
}
