using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Logging;

namespace Xmana.Tests
{
    internal sealed class FakeTransport : IHttpTransport
    {
        private readonly object _gate = new object();
        private readonly List<TransportRequest> _requests = new List<TransportRequest>();

        public FakeTransport(Func<TransportRequest, int, CancellationToken, Task<TransportResponse>> handler)
        {
            Handler = handler;
        }

        public Func<TransportRequest, int, CancellationToken, Task<TransportResponse>> Handler { get; set; }

        public IReadOnlyList<TransportRequest> Requests
        {
            get
            {
                lock (_gate)
                {
                    return _requests.ToArray();
                }
            }
        }

        public int Count => Requests.Count;

        public static FakeTransport Always(string body, int status = 200) =>
            new FakeTransport((r, n, t) => Task.FromResult(new TransportResponse(status, body)));

        /// <summary>Answers by relative path suffix match (first rule whose key is contained in the URL wins).</summary>
        public static FakeTransport Routes(params KeyValuePair<string, string>[] routes) =>
            new FakeTransport((r, n, t) =>
            {
                var url = r.Uri.AbsoluteUri;
                foreach (var route in routes)
                {
                    if (url.Contains(route.Key))
                    {
                        return Task.FromResult(new TransportResponse(200, route.Value));
                    }
                }

                return Task.FromResult(new TransportResponse(404, "{}"));
            });

        public Task<TransportResponse> SendAsync(TransportRequest request, CancellationToken cancellationToken)
        {
            int number;
            lock (_gate)
            {
                _requests.Add(request);
                number = _requests.Count;
            }

            return Handler(request, number, cancellationToken);
        }
    }

    internal sealed class FakeClock : IClock
    {
        public DateTime Now { get; set; } = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => Now;

        public void Advance(TimeSpan span) => Now += span;
    }

    internal sealed class RecordingDelayer : IDelayer
    {
        public List<TimeSpan> Delays { get; } = new List<TimeSpan>();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Delays)
            {
                Delays.Add(delay);
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class ListLogger : IXmanaLogger
    {
        public List<string> Messages { get; } = new List<string>();

        public void Log(LogLevel level, string message)
        {
            lock (Messages)
            {
                Messages.Add(message);
            }
        }
    }

    internal static class Make
    {
        public static ApiHttpClient Http(IHttpTransport transport, IXmanaLogger? logger = null, int attempts = 3, TimeSpan? timeout = null, RecordingDelayer? delayer = null)
        {
            var options = new HttpClientOptions
            {
                Timeout = timeout ?? TimeSpan.FromSeconds(5),
                RetryPolicy = new RetryPolicy(attempts, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                MaxConcurrentRequests = 4,
            };
            return new ApiHttpClient(transport, options, logger, delayer ?? new RecordingDelayer());
        }

        public static readonly Uri ApiUri = new Uri("https://cinemana.shabakaty.cc/api/android/faqQuestion");
    }

    internal static class Json
    {
        public const string Video = @"{
  ""nb"": ""1119122"", ""en_title"": ""Detective Conan"", ""ar_title"": ""المحقق كونان"",
  ""stars"": ""7.5"", ""year"": ""1996"", ""kind"": ""2"", ""season"": ""3"", ""episodeNummer"": ""12"",
  ""rootSeries"": ""1119119"", ""img"": ""https://example.invalid/i.jpg"", ""imgThumb"": ""https://example.invalid/t.jpg"",
  ""imgObjUrl"": ""https://signed.invalid/i?sig=SECRET"", ""imgMediumThumbObjUrl"": ""https://signed.invalid/m?sig=SECRET"",
  ""duration"": ""1500"", ""hasIntroSkipping"": ""true"", ""showComments"": 1, ""videoCommentsNumber"": ""4"", ""videoViewsNumber"": ""12345"",
  ""categories"": [ { ""en_title"": ""Animation"", ""ar_title"": ""رسوم"" }, { ""en_title"": ""Crime"" } ],
  ""actorsInfo"": [ { ""nb"": ""77"", ""name"": ""Minami Takayama"", ""role"": ""Actor"", ""staff_img"": ""https://example.invalid/s.jpg"" } ],
  ""directorsInfo"": [], ""writersInfo"": null, ""videoLanguages"": { ""x"": 1 }
}";
    }
}
