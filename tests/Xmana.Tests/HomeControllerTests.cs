using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Caching;
using Xmana.Core.Home;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Tests
{
    internal sealed class FakeHomeSource : IHomeDataSource
    {
        public Func<Task<ApiResult<IReadOnlyList<VideoSummary>>>> Banner { get; set; } = () => Ok();

        public Func<Task<ApiResult<IReadOnlyList<VideoSummary>>>> Featured { get; set; } = () => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> LatestMovies { get; set; } = p => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> LatestSeries { get; set; } = p => Ok();

        public Func<Task<ApiResult<IReadOnlyList<VideoSummary>>>> Newly { get; set; } = () => Ok();

        public Func<Task<ApiResult<IReadOnlyList<VideoSummary>>>> Popular { get; set; } = () => Ok();

        public Func<string, Task<ApiResult<IReadOnlyList<VideoGroup>>>> Groups { get; set; } =
            lang => Task.FromResult(new ApiResult<IReadOnlyList<VideoGroup>>(new VideoGroup[0], DataOrigin.Network, null));

        public Func<string, int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> GroupPage { get; set; } = (g, p) => Ok();

        public List<string> Calls { get; } = new List<string>();

        public List<string> Languages { get; } = new List<string>();

        public event EventHandler<string>? BackgroundRefreshed;

        public void RaiseRefreshed(string key) => BackgroundRefreshed?.Invoke(this, key);

        public static Task<ApiResult<IReadOnlyList<VideoSummary>>> Ok(DataOrigin origin = DataOrigin.Network, params string[] ids) =>
            Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(Videos(ids), origin, null));

        public static IReadOnlyList<VideoSummary> Videos(params string[] ids) =>
            VideoMapper.ToSummaries(ids.Select(id => JsonParser.Parse("{\"nb\":\"" + id + "\",\"en_title\":\"T" + id + "\",\"img\":\"https://img.invalid/" + id + ".jpg\"}")));

        public static ApiException Fail(int status = 500) => new ApiException(ApiErrorKind.HttpStatus, "x", status, "x");

        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetBannerAsync(RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("banner:" + priority);
            return Banner();
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetFeaturedMoviesAsync(RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("featured:" + priority);
            return Featured();
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestMoviesAsync(int page, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("latestMovies:" + page);
            return LatestMovies(page);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetLatestSeriesAsync(int page, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("latestSeries:" + page);
            return LatestSeries(page);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetNewlyAddedAsync(int count, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("newly:" + count + ":" + priority);
            return Newly();
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetPopularAsync(int page, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("popular:" + page);
            return Popular();
        }

        public Task<ApiResult<IReadOnlyList<VideoGroup>>> GetGroupsAsync(string lang, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("groups");
            lock (Languages)
            {
                Languages.Add(lang);
            }

            return Groups(lang);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetGroupPageAsync(string groupId, int page, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("groupPage:" + groupId + ":" + page);
            return GroupPage(groupId, page);
        }
    }

    [TestClass]
    public class HomeControllerTests
    {
        private static HomeController Create(FakeHomeSource source, string lang = "en") =>
            new HomeController(source, () => lang, new RecordingDelayer());

        private static Task<ApiResult<IReadOnlyList<VideoGroup>>> GroupsOf(params VideoGroup[] groups) =>
            Task.FromResult(new ApiResult<IReadOnlyList<VideoGroup>>(groups, DataOrigin.Network, null));

        private static VideoGroup Group(string id, string? title, bool paged, params string[] ids) =>
            new VideoGroup(id, title, paged, FakeHomeSource.Videos(ids), new Newtonsoft.Json.Linq.JObject());

        [TestMethod]
        public async Task EverythingEmpty_IsAnEmptyState_NotAnError()
        {
            var home = Create(new FakeHomeSource());
            Assert.AreEqual(HomeState.Loading, home.State);
            await home.LoadAsync(CancellationToken.None);

            Assert.AreEqual(HomeState.Empty, home.State);
            Assert.AreEqual(0, home.Sections.Count);
            Assert.AreEqual(0, home.Hero.Count);
        }

        [TestMethod]
        public async Task EverythingFails_IsAnErrorState()
        {
            var source = new FakeHomeSource
            {
                Banner = () => throw FakeHomeSource.Fail(),
                Featured = () => throw FakeHomeSource.Fail(),
                LatestMovies = p => throw FakeHomeSource.Fail(),
                LatestSeries = p => throw FakeHomeSource.Fail(),
                Newly = () => throw FakeHomeSource.Fail(),
                Popular = () => throw FakeHomeSource.Fail(),
                Groups = l => throw FakeHomeSource.Fail(),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HomeState.Error, home.State);
        }

        [TestMethod]
        public async Task OneFailingSection_DoesNotBlockTheOthers()
        {
            var source = new FakeHomeSource
            {
                Featured = () => throw FakeHomeSource.Fail(503),
                LatestMovies = p => FakeHomeSource.Ok(DataOrigin.Network, "1", "2"),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.AreEqual(HomeState.Content, home.State);
            CollectionAssert.AreEqual(new[] { "latestMovies" }, home.Sections.Select(s => s.Id).ToArray());
        }

        [TestMethod]
        public async Task EmptySections_AreHidden_AndOrderIsFixed()
        {
            var source = new FakeHomeSource
            {
                Banner = () => FakeHomeSource.Ok(DataOrigin.Network, "b1"),
                Featured = () => FakeHomeSource.Ok(DataOrigin.Network, "f1"),
                LatestMovies = p => FakeHomeSource.Ok(DataOrigin.Network, "m1"),
                LatestSeries = p => FakeHomeSource.Ok(),
                Newly = () => FakeHomeSource.Ok(DataOrigin.Network, "n1"),
                Popular = () => FakeHomeSource.Ok(),
                Groups = l => GroupsOf(Group("404", "Emmy Winners", true, "g1"), Group("9", "Empty group", true), Group("8", null, true, "x")),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "featuredMovies", "latestMovies", "group:404", "newlyAdded" },
                home.Sections.Select(s => s.Id).ToArray(),
                "empty latestSeries/popular, the empty group and the untitled group must not appear");
            Assert.AreEqual("Emmy Winners", home.Sections.Single(s => s.Kind == HomeSectionKind.Group).Title);
            Assert.AreEqual(1, home.Hero.Count);
        }

        [TestMethod]
        public async Task PopularEmpty_IsNotShown_PopularWithData_IsShown()
        {
            var source = new FakeHomeSource { Popular = () => FakeHomeSource.Ok(DataOrigin.Network, "p1", "p2") };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(2, home.Sections.Single(s => s.Kind == HomeSectionKind.Popular).Items.Count);
        }

        [TestMethod]
        public async Task Hero_KeepsOnlyItemsWithArtwork()
        {
            var noImage = VideoMapper.ToSummaries(new[] { JsonParser.Parse("{\"nb\":\"1\",\"en_title\":\"no art\"}"), JsonParser.Parse("{\"nb\":\"2\",\"img\":\"http://insecure.invalid/a.jpg\"}") });
            var source = new FakeHomeSource
            {
                Banner = () => Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(noImage.Concat(FakeHomeSource.Videos("3")).ToList(), DataOrigin.Network, null)),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "3" }, home.Hero.Select(h => h.Id).ToArray());
        }

        [TestMethod]
        public async Task HeroIsRequestedWithTheHighestPriority_AndNewlyUsesTheDocumentedCount()
        {
            var source = new FakeHomeSource();
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.IsTrue(source.Calls.Contains("banner:Hero"));
            Assert.IsTrue(source.Calls.Contains("featured:Primary"));
            Assert.IsTrue(source.Calls.Contains("newly:12:Secondary"));
            Assert.IsTrue(source.Calls.Contains("popular:0"));
            Assert.IsTrue(source.Calls.Contains("latestMovies:0"));
        }

        [TestMethod]
        public async Task GroupsUseTheCurrentLanguage_AndReloadAfterALanguageChange()
        {
            var language = "en";
            var source = new FakeHomeSource();
            var home = new HomeController(source, () => language, new RecordingDelayer());
            await home.LoadAsync(CancellationToken.None);
            language = "ar";
            await home.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "en", "ar" }, source.Languages);
        }

        [TestMethod]
        public async Task Sections_ArriveProgressively_AndChangedIsRaisedForEach()
        {
            var gate = new TaskCompletionSource<bool>();
            var source = new FakeHomeSource
            {
                Banner = () => FakeHomeSource.Ok(DataOrigin.Network, "b1"),
                Featured = async () =>
                {
                    await gate.Task;
                    return await FakeHomeSource.Ok(DataOrigin.Network, "f1");
                },
            };
            var home = Create(source);
            var changes = 0;
            home.Changed += (s, e) => Interlocked.Increment(ref changes);

            var loading = home.LoadAsync(CancellationToken.None);
            for (var i = 0; i < 400 && home.Hero.Count == 0; i++)
            {
                await Task.Delay(5);
            }

            Assert.AreEqual(1, home.Hero.Count, "hero must be visible before the slow section finishes");
            Assert.AreEqual(HomeState.Content, home.State);
            Assert.IsFalse(loading.IsCompleted);
            Assert.AreEqual(0, home.Sections.Count);

            gate.SetResult(true);
            await loading;
            Assert.AreEqual(1, home.Sections.Count);
            Assert.IsTrue(changes >= 2);
        }

        [TestMethod]
        public async Task Refresh_KeepsContentOnScreen_WhenARequestFails()
        {
            var failNext = false;
            var source = new FakeHomeSource
            {
                Featured = () => failNext ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(DataOrigin.Network, "f1"),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, home.Sections.Count);

            failNext = true;
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HomeState.Content, home.State);
            Assert.AreEqual(1, home.Sections.Count, "previously loaded data must not vanish because a refresh failed");
        }

        [TestMethod]
        public async Task RetryAfterError_Recovers()
        {
            var failing = true;
            var source = new FakeHomeSource
            {
                Featured = () => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(DataOrigin.Network, "f1"),
                LatestMovies = p => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(DataOrigin.Network, "m1"),
                LatestSeries = p => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(),
                Banner = () => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(),
                Newly = () => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(),
                Popular = () => failing ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(),
                Groups = l => failing ? throw FakeHomeSource.Fail() : GroupsOf(),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HomeState.Error, home.State);

            failing = false;
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HomeState.Content, home.State);
        }

        [TestMethod]
        public async Task OutdatedCacheFallback_IsReported()
        {
            var source = new FakeHomeSource { Featured = () => FakeHomeSource.Ok(DataOrigin.CacheAfterError, "f1") };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.IsTrue(home.IsShowingOutdatedData);

            var fresh = Create(new FakeHomeSource { Featured = () => FakeHomeSource.Ok(DataOrigin.CacheFresh, "f1") });
            await fresh.LoadAsync(CancellationToken.None);
            Assert.IsFalse(fresh.IsShowingOutdatedData);
        }

        [TestMethod]
        public async Task Cancellation_Propagates()
        {
            var home = Create(new FakeHomeSource());
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                OperationCanceledException? caught = null;
                try
                {
                    await home.LoadAsync(cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    caught = ex; // exact subclass depends on where cancellation is observed
                }

                Assert.IsNotNull(caught);
            }
        }

        // ---- Featured Series (observed behavior: videoGroups group 2) --------------------------

        [TestMethod]
        public async Task FeaturedSeries_IsGroupTwo_PlacedRightAfterFeaturedMovies_WithItsApiTitle()
        {
            var source = new FakeHomeSource
            {
                Featured = () => FakeHomeSource.Ok(DataOrigin.Network, "m1"),
                LatestMovies = p => FakeHomeSource.Ok(DataOrigin.Network, "lm1"),
                Groups = l => GroupsOf(Group("404", "Emmy", true, "g1"), Group("2", "Featured Series", true, "s1", "s2")),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "featuredMovies", "featuredSeries", "latestMovies", "group:404" },
                home.Sections.Select(x => x.Id).ToArray());
            var rail = home.Sections.Single(x => x.Kind == HomeSectionKind.FeaturedSeries);
            Assert.AreEqual("Featured Series", rail.Title);
            Assert.AreEqual("2", rail.GroupId);
            Assert.AreEqual(2, rail.Items.Count);
            Assert.IsTrue(rail.CanLoadMore);
            Assert.AreEqual(0, home.Sections.Count(x => x.Id == "group:2"), "group 2 must not also appear as an anonymous group");
        }

        [TestMethod]
        public async Task FeaturedSeries_WithoutFeaturedMovies_StillAppearsFirst()
        {
            var source = new FakeHomeSource { Groups = l => GroupsOf(Group("2", "Featured Series", true, "s1")) };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HomeSectionKind.FeaturedSeries, home.Sections.First().Kind);
        }

        [TestMethod]
        public async Task FeaturedSeries_LocalizedTitle_IsKeptAsSent()
        {
            var home = Create(new FakeHomeSource { Groups = l => GroupsOf(Group("2", l == "ar" ? "مسلسلات مميزة" : "Featured Series", true, "s1")) }, "ar");
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual("مسلسلات مميزة", home.Sections.Single().Title);
        }

        [TestMethod]
        public async Task FeaturedSeries_BlankTitle_FallsBackToTheLocalizedResource_ByHavingNoTitle()
        {
            var home = Create(new FakeHomeSource { Groups = l => GroupsOf(Group("2", "  ", true, "s1")) });
            await home.LoadAsync(CancellationToken.None);
            var rail = home.Sections.Single();
            Assert.AreEqual(HomeSectionKind.FeaturedSeries, rail.Kind);
            Assert.IsNull(rail.Title, "the UI then uses its localized 'Featured Series' resource");
        }

        [TestMethod]
        public async Task FeaturedSeries_IsOptional_AbsentOrEmptyGroupTwo_ShowsNoRail()
        {
            var absent = Create(new FakeHomeSource { Groups = l => GroupsOf(Group("404", "Emmy", true, "g1")) });
            await absent.LoadAsync(CancellationToken.None);
            Assert.AreEqual(0, absent.Sections.Count(x => x.Kind == HomeSectionKind.FeaturedSeries));

            var empty = Create(new FakeHomeSource { Groups = l => GroupsOf(Group("2", "Featured Series", true)) });
            await empty.LoadAsync(CancellationToken.None);
            Assert.AreEqual(0, empty.Sections.Count);
            Assert.AreEqual(HomeState.Empty, empty.State);
        }

        [TestMethod]
        public async Task FeaturedSeries_Pagination_UsesTheGroupPaginationEndpointForGroupTwo()
        {
            var source = new FakeHomeSource
            {
                Groups = l => GroupsOf(Group("2", "Featured Series", true, "s1", "s2")),
                GroupPage = (g, p) => g == "2" && p == 1 ? FakeHomeSource.Ok(DataOrigin.Network, "s2", "s3") : FakeHomeSource.Ok(),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.IsTrue(await home.LoadMoreAsync("featuredSeries", CancellationToken.None));
            CollectionAssert.AreEqual(new[] { "s1", "s2", "s3" }, home.Sections.Single().Items.Select(i => i.Id).ToArray());
            Assert.IsTrue(source.Calls.Contains("groupPage:2:1"));
        }

        [TestMethod]
        public async Task FeaturedSeries_WithoutPagination_DoesNotRequestMore()
        {
            var source = new FakeHomeSource { Groups = l => GroupsOf(Group("2", "Featured Series", false, "s1")) };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.IsFalse(await home.LoadMoreAsync("featuredSeries", CancellationToken.None));
            Assert.AreEqual(0, source.Calls.Count(c => c.StartsWith("groupPage")));
        }

        [TestMethod]
        public void FeaturedSeriesGroupId_IsTheDocumentedObservedValue()
        {
            Assert.AreEqual("2", HomeController.FeaturedSeriesGroupId);
        }

        // ---- load more -------------------------------------------------------------------------

        [TestMethod]
        public async Task LoadMore_Group_FetchesNextPage_AndAppendsUniqueItems()
        {
            var source = new FakeHomeSource
            {
                Groups = l => GroupsOf(Group("404", "Emmy", true, "1", "2")),
                GroupPage = (g, p) => p == 1 ? FakeHomeSource.Ok(DataOrigin.Network, "2", "3", "4") : FakeHomeSource.Ok(),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.IsTrue(await home.LoadMoreAsync("group:404", CancellationToken.None));
            CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" }, home.Sections.Single().Items.Select(i => i.Id).ToArray());
            Assert.IsTrue(source.Calls.Contains("groupPage:404:1"));

            Assert.IsFalse(await home.LoadMoreAsync("group:404", CancellationToken.None), "an empty page ends pagination");
            Assert.IsFalse(home.Sections.Single().CanLoadMore);
            Assert.IsTrue(source.Calls.Contains("groupPage:404:2"));
            Assert.IsFalse(await home.LoadMoreAsync("group:404", CancellationToken.None));
            Assert.AreEqual(1, source.Calls.Count(c => c == "groupPage:404:2"), "no further requests once pagination ended");
        }

        [TestMethod]
        public async Task LoadMore_LatestMovies_UsesPageNumbers()
        {
            var source = new FakeHomeSource
            {
                LatestMovies = p => p == 0 ? FakeHomeSource.Ok(DataOrigin.Network, "1", "2") : FakeHomeSource.Ok(DataOrigin.Network, "3"),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            Assert.IsTrue(await home.LoadMoreAsync("latestMovies", CancellationToken.None));
            Assert.IsTrue(source.Calls.Contains("latestMovies:1"));
            Assert.AreEqual(3, home.Sections.Single().Items.Count);
        }

        [TestMethod]
        public async Task LoadMore_NotPageable_UnknownOrUngroupedGroup_IsRefused()
        {
            var source = new FakeHomeSource
            {
                Featured = () => FakeHomeSource.Ok(DataOrigin.Network, "f1"),
                Groups = l => GroupsOf(Group("5", "No paging", false, "1")),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.IsFalse(await home.LoadMoreAsync("featuredMovies", CancellationToken.None));
            Assert.IsFalse(await home.LoadMoreAsync("group:5", CancellationToken.None));
            Assert.IsFalse(await home.LoadMoreAsync("nope", CancellationToken.None));
            Assert.AreEqual(0, source.Calls.Count(c => c.StartsWith("groupPage")));
        }

        [TestMethod]
        public async Task LoadMore_Failure_KeepsTheRailAndAllowsARetry()
        {
            var fail = true;
            var source = new FakeHomeSource
            {
                Groups = l => GroupsOf(Group("404", "Emmy", true, "1")),
                GroupPage = (g, p) => fail ? throw FakeHomeSource.Fail() : FakeHomeSource.Ok(DataOrigin.Network, "2"),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            Assert.IsFalse(await home.LoadMoreAsync("group:404", CancellationToken.None));
            Assert.IsTrue(home.Sections.Single().CanLoadMore);
            Assert.AreEqual(1, home.Sections.Single().Items.Count);

            fail = false;
            Assert.IsTrue(await home.LoadMoreAsync("group:404", CancellationToken.None));
            Assert.AreEqual(2, home.Sections.Single().Items.Count);
        }

        [TestMethod]
        public async Task LoadMore_ConcurrentCallsForTheSameRail_FetchOnce()
        {
            var gate = new TaskCompletionSource<bool>();
            var source = new FakeHomeSource
            {
                Groups = l => GroupsOf(Group("404", "Emmy", true, "1")),
                GroupPage = async (g, p) =>
                {
                    await gate.Task;
                    return await FakeHomeSource.Ok(DataOrigin.Network, "2");
                },
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);

            var first = home.LoadMoreAsync("group:404", CancellationToken.None);
            var second = await home.LoadMoreAsync("group:404", CancellationToken.None);
            Assert.IsFalse(second);
            gate.SetResult(true);
            Assert.IsTrue(await first);
            Assert.AreEqual(1, source.Calls.Count(c => c.StartsWith("groupPage")));
        }

        // ---- background refresh ----------------------------------------------------------------

        [TestMethod]
        public async Task BackgroundRefresh_UpdatesChangedSections_WithoutResettingExtendedRails()
        {
            var version = 1;
            var source = new FakeHomeSource
            {
                Groups = l => GroupsOf(Group("404", "Emmy", true, "1", "2")),
                GroupPage = (g, p) => FakeHomeSource.Ok(DataOrigin.Network, "3"),
                Featured = () => version == 1 ? FakeHomeSource.Ok(DataOrigin.CacheStale, "f1") : FakeHomeSource.Ok(DataOrigin.Network, "f9"),
            };
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            await home.LoadMoreAsync("group:404", CancellationToken.None);
            Assert.AreEqual(3, home.Sections.Single(s => s.Id == "group:404").Items.Count);

            version = 2;
            source.RaiseRefreshed("featuredMovies");
            source.RaiseRefreshed("featuredMovies");
            await home.WaitForQuietReloadAsync();

            Assert.AreEqual("f9", home.Sections.Single(s => s.Id == "featuredMovies").Items.Single().Id);
            Assert.AreEqual(3, home.Sections.Single(s => s.Id == "group:404").Items.Count, "an unchanged rail keeps the pages the user already loaded");
        }

        [TestMethod]
        public async Task BackgroundRefresh_Bursts_AreDebounced()
        {
            var source = new FakeHomeSource();
            var home = Create(source);
            await home.LoadAsync(CancellationToken.None);
            var before = source.Calls.Count(c => c.StartsWith("banner"));

            for (var i = 0; i < 5; i++)
            {
                source.RaiseRefreshed("k" + i);
            }

            await home.WaitForQuietReloadAsync();
            Assert.AreEqual(before + 1, source.Calls.Count(c => c.StartsWith("banner")), "five refresh events cause a single reload");
        }
    }
}
