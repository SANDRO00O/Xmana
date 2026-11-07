using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Tests
{
    [TestClass]
    public class CinemanaApiTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init() => _dir = Path.Combine(Path.GetTempPath(), "xmana-api-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private sealed class Harness
        {
            public Harness(FakeTransport transport, IResponseCache? cache = null)
            {
                Transport = transport;
                Clock = new FakeClock();
                Cache = cache ?? new MemoryResponseCache();
                Source = new CachedJsonSource(Cache, CachePolicy.Default, Clock, new ListLogger());
                Api = new CinemanaApi(Make.Http(transport), Source, new CinemanaApiOptions { Level = 0 });
            }

            public FakeTransport Transport { get; }

            public FakeClock Clock { get; }

            public IResponseCache Cache { get; }

            public CachedJsonSource Source { get; }

            public CinemanaApi Api { get; }

            public string LastUrl => Transport.Requests.Last().Uri.AbsoluteUri;
        }

        private static Harness With(string body, int status = 200) => new Harness(FakeTransport.Always(body, status));

        // ---- requests use the documented routes ------------------------------------------------

        [TestMethod]
        public async Task GetVideoInfo_RequestsTheDocumentedRoute_AndMapsDetails()
        {
            var h = With(Json.Video);
            var result = await h.Api.GetVideoInfoAsync("1119122");

            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/allVideoInfo/id/1119122", h.LastUrl);
            Assert.AreEqual(DataOrigin.Network, result.Origin);
            Assert.AreEqual("Detective Conan", result.Value!.Summary.EnTitle);
            Assert.AreEqual(ApiHttpMethod.Get, h.Transport.Requests[0].Method);
        }

        [TestMethod]
        public async Task Level_IsAppliedToLevelEndpoints()
        {
            var h = With("[]");
            await h.Api.GetBannerAsync();
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/banner/level/0", h.LastUrl);
            await h.Api.GetLatestMoviesAsync(1);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/latestMovies/level/0/itemsPerPage/24/page/1/", h.LastUrl);
            await h.Api.GetPopularAsync(PopularPeriod.Week, 0);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/popularFilms/kind/W/level/0/page/0", h.LastUrl);
        }

        [TestMethod]
        public async Task Search_UsesBase64UrlKeyword_AndTrimsInput()
        {
            var h = With("{\"info\":[{\"nb\":\"1\",\"en_title\":\"Detective Conan\"}]}");
            var result = await h.Api.SearchAsync("  Conan ", 12, 0);

            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/video/V/2/itemsPerPage/20/video_title_search/Q29uYW4/itemsPerPage/12/pageNumber/0/level/0", h.LastUrl);
            Assert.AreEqual(1, result.Value.Count);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("   ")]
        public async Task Search_BlankKeyword_SendsNoRequest_AndReturnsEmpty(string keyword)
        {
            var h = With("[]");
            var result = await h.Api.SearchAsync(keyword, 12, 0);
            Assert.AreEqual(0, result.Value.Count);
            Assert.AreEqual(0, h.Transport.Count);
        }

        [TestMethod]
        public async Task AdvancedSearch_SendsOnlyTheSelectedFilters()
        {
            var h = With("{\"info\":[]}");
            await h.Api.AdvancedSearchAsync(new AdvancedSearchFilters { VideoTitle = "Conan", Type = SearchType.Movies, CategoryIds = new[] { "57", "60" } });
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/AdvancedSearch?level=0&videoTitle=Conan&category_id=57,60&type=movies", h.LastUrl);
        }

        [TestMethod]
        public async Task CategoryEndpoints_KeepTheirOwnPaginationModels()
        {
            var h = With("{\"info\":[]}");
            await h.Api.GetVideosByCategoryAsync("84", ListingKind.Movies, ListingSort.Desc, 30);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/videosByCategory?categoryID=84&orderby=desc&videoKind=1&offset=30&level=0", h.LastUrl);

            await h.Api.GetVideoListingAsync(ListingKind.Series, ListingSort.StarsDesc, 12, 2);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/video/V/2/itemsPerPage/12/level/0/videoKind/2/sortParam/stars_desc/pageNumber/2", h.LastUrl);

            await h.Api.GetNewlyVideosAsync(12);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/newlyVideosItems/level/0/offset/12/", h.LastUrl);
        }

        [TestMethod]
        public async Task InvalidArguments_FailBeforeAnyRequest()
        {
            var h = With("[]");
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => h.Api.GetVideoInfoAsync(" "));
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => h.Api.GetVideosByCategoryAsync("84", ListingKind.Movies, "newest", 0));
            await Assert.ThrowsExceptionAsync<ArgumentOutOfRangeException>(() => h.Api.GetLatestMoviesAsync(-1));
            Assert.AreEqual(0, h.Transport.Count);
        }

        // ---- results ---------------------------------------------------------------------------

        [TestMethod]
        public async Task EmptyListResponses_AreEmptyResults_NotErrors()
        {
            var h = With("[]");
            Assert.AreEqual(0, (await h.Api.GetPopularAsync(PopularPeriod.Month, 0)).Value.Count);
            Assert.AreEqual(0, (await h.Api.GetCollectionIdsAsync()).Value.Count);
            Assert.AreEqual(0, (await h.Api.GetCollectionVideosAsync("1")).Value.Count);
            Assert.AreEqual(0, (await h.Api.GetFaqAsync()).Value.Count);
            Assert.AreEqual(0, (await h.Api.GetCommentsAsync("1", 10, 0)).Value.Count);
        }

        [TestMethod]
        public async Task GroupsAndEpisodes_MapFromTheirDocumentedShapes()
        {
            var groups = await With("{\"groups\":[{\"groupsID\":\"404\",\"title\":\"T\",\"hasPagination\":true,\"content\":[{\"nb\":\"1\"}]}]}").Api.GetGroupsAsync("en");
            Assert.AreEqual("404", groups.Value.Single().GroupId);

            var episodes = await With("[{\"nb\":\"b\",\"season\":\"1\",\"episodeNummer\":\"2\"},{\"nb\":\"a\",\"season\":\"1\",\"episodeNummer\":\"1\"}]").Api.GetEpisodesAsync("1119119");
            var grouped = CatalogMapper.GroupBySeason(episodes.Value);
            CollectionAssert.AreEqual(new[] { "a", "b" }, grouped.Single().Episodes.Select(e => e.Id).ToArray());
        }

        [TestMethod]
        public async Task MalformedAndHttpErrors_SurfaceAsApiException()
        {
            var malformed = await Assert.ThrowsExceptionAsync<ApiException>(() => With("{ nope").Api.GetFaqAsync());
            Assert.AreEqual(ApiErrorKind.Malformed, malformed.Kind);

            var http = await Assert.ThrowsExceptionAsync<ApiException>(() => With("{}", 404).Api.GetFaqAsync());
            Assert.AreEqual(404, http.StatusCode);
        }

        [TestMethod]
        public async Task Cancellation_Propagates()
        {
            var h = With("[]");
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => h.Api.GetBannerAsync(RequestPriority.Hero, cts.Token));
            }

            Assert.AreEqual(0, h.Transport.Count);
        }

        // ---- caching behaviour through the API -------------------------------------------------

        [TestMethod]
        public async Task SecondCall_IsServedFromCache_WithoutANewRequest()
        {
            var h = With(Json.Video);
            await h.Api.GetVideoInfoAsync("1119122");
            var second = await h.Api.GetVideoInfoAsync("1119122");

            Assert.AreEqual(DataOrigin.CacheFresh, second.Origin);
            Assert.IsTrue(second.IsFromCache);
            Assert.IsFalse(second.MayBeOutdated);
            Assert.AreEqual(1, h.Transport.Count);
            Assert.AreEqual("Detective Conan", second.Value!.Summary.EnTitle);
        }

        [TestMethod]
        public async Task DifferentIdsAndParameters_DoNotShareCacheEntries()
        {
            var h = With("[]");
            await h.Api.GetLatestMoviesAsync(0);
            await h.Api.GetLatestMoviesAsync(1);
            await h.Api.GetLatestSeriesAsync(0);
            Assert.AreEqual(3, h.Transport.Count);
        }

        [TestMethod]
        public async Task OfflineAfterExpiry_ShowsOlderCopy_FlaggedAsOutdated()
        {
            var transport = new FakeTransport((r, n, t) => n == 1
                ? Task.FromResult(new TransportResponse(200, "[{\"nb\":\"1\",\"en_title\":\"Cached\"}]"))
                : throw new System.Net.Http.HttpRequestException("offline"));
            var h = new Harness(transport);

            await h.Api.GetLatestMoviesAsync(0);
            h.Clock.Advance(TimeSpan.FromDays(2)); // beyond ttl + stale window, inside the error fallback

            var offline = await h.Api.GetLatestMoviesAsync(0);
            Assert.AreEqual(DataOrigin.CacheAfterError, offline.Origin);
            Assert.IsTrue(offline.MayBeOutdated);
            Assert.AreEqual("Cached", offline.Value.Single().EnTitle);
        }

        [TestMethod]
        public async Task PlaybackData_IsNeverCached_SoSignedUrlsAreAlwaysFresh()
        {
            var json = "[{\"resolution\":\"720p\",\"videoUrl\":\"https://cdn.invalid/a.mp4?sig=SECRET\"}]";
            var cache = new MemoryResponseCache();
            var h = new Harness(FakeTransport.Always(json), cache);

            await h.Api.GetVideoFilesAsync("1");
            await h.Api.GetVideoFilesAsync("1");
            await h.Api.GetTranslationsAsync("1");
            await h.Api.GetSkipInfoAsync("1");
            await h.Api.GetThumbnailsAsync("1");
            await h.Api.GetParentalStatusAsync("1");
            await h.Api.GetCommentsAsync("1", 10, 0);

            Assert.AreEqual(7, h.Transport.Count, "every playback-related call must hit the network");
            Assert.AreEqual(0, ((MemoryResponseCache)cache).Count);
        }

        [TestMethod]
        public async Task OnDisk_NothingWithASignedUrlIsEverPersisted()
        {
            var disk = new FileResponseCache(_dir);
            var h = new Harness(FakeTransport.Always(Json.Video), disk);
            await h.Api.GetVideoInfoAsync("1119122");
            await h.Api.GetRelatedVideosAsync("1119122", "2");

            var files = Directory.GetFiles(_dir);
            Assert.IsTrue(files.Length >= 2);
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains("SECRET"), file);
                Assert.IsFalse(text.Contains("ObjUrl"), file);
            }
        }

        [TestMethod]
        public async Task CacheSurvivesRestart_WhenBackedByDisk()
        {
            var first = new Harness(FakeTransport.Always(Json.Video), new FileResponseCache(_dir));
            await first.Api.GetVideoInfoAsync("1119122");

            var offline = new FakeTransport((r, n, t) => throw new System.Net.Http.HttpRequestException("offline"));
            var second = new Harness(offline, new FileResponseCache(_dir));
            var result = await second.Api.GetVideoInfoAsync("1119122");

            Assert.AreEqual(DataOrigin.CacheFresh, result.Origin);
            Assert.AreEqual(0, offline.Count);
            Assert.AreEqual("Detective Conan", result.Value!.Summary.EnTitle);
        }

        // ---- external services -----------------------------------------------------------------

        [TestMethod]
        public async Task Ratings_NoTargets_SendsNothing()
        {
            var h = With("{}");
            Assert.IsFalse((await h.Api.GetRatingSummaryAsync(new RatingTarget[0])).HasData);
            Assert.IsFalse((await h.Api.GetRatingSummaryAsync(null!)).HasData);
            Assert.AreEqual(0, h.Transport.Count);
        }

        [TestMethod]
        public async Task Ratings_PostTheVerifiedBody_WithMovieAndEpisodeTargets_AndHideEmptyResults()
        {
            var h = With("{\"items\":[],\"totalCount\":0}");
            var summary = await h.Api.GetRatingSummaryAsync(new[]
            {
                RatingTarget.For("1", "0", "192319"),
                RatingTarget.For("2", "1119119", "1119122"),
                RatingTarget.For("1", "0", "192319"), // duplicate is sent once
            });

            var request = h.Transport.Requests.Single();
            Assert.AreEqual(ApiHttpMethod.Post, request.Method);
            Assert.AreEqual("https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types", request.Uri.AbsoluteUri);
            var targets = (JArray)JsonParser.Parse(request.Body)["targets"]!;
            Assert.AreEqual(2, targets.Count);
            Assert.AreEqual("192319", (string?)targets[0]["targetId"]);
            Assert.AreEqual("Movie", (string?)targets[0]["targetType"]);
            Assert.AreEqual("1119119", (string?)targets[1]["targetId"]);
            Assert.AreEqual("Episode", (string?)targets[1]["targetType"]);
            Assert.IsFalse(summary.HasData, "an empty rating summary must not be displayed");
        }

        [TestMethod]
        public async Task Ratings_FailureIsAnApiException_NotFakeData()
        {
            await Assert.ThrowsExceptionAsync<ApiException>(() => With("{}", 500).Api.GetRatingSummaryAsync(new[] { RatingTarget.For("1", "0", "5") }));
        }

        [TestMethod]
        public async Task Recommendations_SendTheVerifiedBody_ToTheDocumentedHost()
        {
            var h = With("{\"expirationDate\":\"2026-10-03T00:00:00Z\",\"items\":[{\"nb\":\"7\",\"en_title\":\"Seven\",\"thumb\":\"https://signed.invalid/x?sig=SECRET\"}]}");
            var result = await h.Api.GetRecommendationsAsync(" 3143018 ", " Some Title ");

            var request = h.Transport.Requests.Single();
            Assert.AreEqual(ApiHttpMethod.Post, request.Method);
            Assert.AreEqual("https://recommend.shabakaty.cc/api/recommendation/recommend", request.Uri.AbsoluteUri);
            var body = (JObject)JsonParser.Parse(request.Body);
            Assert.AreEqual(3, body.Count, "exactly MovieId, MovieName, ReProcessIfExpired");
            Assert.AreEqual("3143018", (string?)body["MovieId"]);
            Assert.AreEqual("Some Title", (string?)body["MovieName"]);
            Assert.AreEqual(true, (bool?)body["ReProcessIfExpired"]);

            Assert.IsTrue(result.HasItems);
            Assert.AreEqual("7", result.Items.Single().Id);
            Assert.AreEqual("2026-10-03T00:00:00Z", result.ExpirationDate);
        }

        [TestMethod]
        public async Task Recommendations_AreNeverCached_AndNeverLogged()
        {
            var logger = new ListLogger();
            var transport = FakeTransport.Always("[{\"nb\":\"7\",\"x\":\"https://signed.invalid/x?sig=SECRET\"}]");
            var cache = new MemoryResponseCache();
            var api = new CinemanaApi(Make.Http(transport, logger), new CachedJsonSource(cache, CachePolicy.Default, new FakeClock(), logger));

            await api.GetRecommendationsAsync("1", "T");
            await api.GetRecommendationsAsync("1", "T");

            Assert.AreEqual(2, transport.Count);
            Assert.AreEqual(0, cache.Count);
            foreach (var message in logger.Messages)
            {
                Assert.IsFalse(message.Contains("SECRET"), message);
            }
        }

        [TestMethod]
        public async Task Recommendations_BlankIdIsRejected_EmptyAndUnknownShapesAreEmptyResults()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => With("[]").Api.GetRecommendationsAsync(" ", "T"));
            Assert.IsFalse((await With("[]").Api.GetRecommendationsAsync("1", "T")).HasItems);
            Assert.IsFalse((await With("{}").Api.GetRecommendationsAsync("1", "T")).HasItems);
            Assert.IsFalse((await With("\"odd\"").Api.GetRecommendationsAsync("1", "T")).HasItems);
        }

        [TestMethod]
        public async Task Recommendations_ServiceFailure_IsAnApiException_SoCallersCanFallBack()
        {
            var ex = await Assert.ThrowsExceptionAsync<ApiException>(() => With("{}", 503).Api.GetRecommendationsAsync("1", "T"));
            Assert.AreEqual(503, ex.StatusCode);
        }

        [TestMethod]
        public void PublicSurface_HasNoAuthenticationOrAccountMethods()
        {
            var names = typeof(ICinemanaApi).GetMethods().Select(m => m.Name.ToLowerInvariant()).ToList();
            foreach (var forbidden in new[] { "login", "register", "password", "token", "oauth", "favorite", "watchlater", "subscribe", "notification" })
            {
                Assert.IsFalse(names.Any(n => n.Contains(forbidden)), "unexpected API method containing '" + forbidden + "'");
            }
        }
    }
}
