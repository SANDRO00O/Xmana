using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api;
using Xmana.Core.Caching;
using Xmana.Core.Discovery;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Tests
{
    internal sealed class FakeDiscoverySource : IDiscoveryDataSource
    {
        public List<string> Calls { get; } = new List<string>();

        public Func<string, int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> Search { get; set; } = (k, p) => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> Listing { get; set; } = p => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> ByCategory { get; set; } = o => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> ByCategoryAndLanguage { get; set; } = o => Ok();

        public Func<int, Task<ApiResult<IReadOnlyList<VideoSummary>>>> Advanced { get; set; } = p => Ok();

        public Func<Task<ApiResult<IReadOnlyList<CategoryInfo>>>> Main { get; set; } = () => Cats();

        public Func<Task<ApiResult<IReadOnlyList<CategoryInfo>>>> Sub { get; set; } = () => Cats();

        public Func<Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>>> Languages { get; set; } =
            () => Task.FromResult(new ApiResult<IReadOnlyList<CategoryLanguageGroup>>(new CategoryLanguageGroup[0], DataOrigin.Network, null));

        public AdvancedSearchFilters? LastAdvanced { get; private set; }

        public static Task<ApiResult<IReadOnlyList<VideoSummary>>> Ok(params string[] ids) =>
            Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(FakeHomeSource.Videos(ids), DataOrigin.Network, null));

        public static Task<ApiResult<IReadOnlyList<VideoSummary>>> OkFrom(DataOrigin origin, params string[] ids) =>
            Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(FakeHomeSource.Videos(ids), origin, null));

        public static Task<ApiResult<IReadOnlyList<CategoryInfo>>> Cats(params string[] idsAndTitles) =>
            Task.FromResult(new ApiResult<IReadOnlyList<CategoryInfo>>(
                idsAndTitles.Select(s => new CategoryInfo(s.Split('=')[0], s.Split('=')[1], null, true, new JObject())).ToList(),
                DataOrigin.Network,
                null));

        public static CategoryLanguageGroup LangGroup(string categoryId, params string[] languages) =>
            new CategoryLanguageGroup(
                categoryId,
                null,
                null,
                languages.Select(l =>
                {
                    var parts = l.Split(':'); // id:movies:series:name
                    return new CategoryLanguage(parts[0], parts[1] == "?" ? (int?)null : int.Parse(parts[1]), parts[2] == "?" ? (int?)null : int.Parse(parts[2]), null, parts[3], new JObject());
                }).ToList(),
                new JObject());

        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetListingAsync(ListingKind kind, string sort, int itemsPerPage, int pageNumber, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("listing:" + kind + ":" + sort + ":" + itemsPerPage + ":" + pageNumber);
            return Listing(pageNumber);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAsync(string categoryId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("category:" + categoryId + ":" + kind + ":" + sort + ":" + offset);
            return ByCategory(offset);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetByCategoryAndLanguageAsync(string categoryId, string languageId, ListingKind kind, string sort, int offset, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("categoryLanguage:" + categoryId + ":" + languageId + ":" + kind + ":" + sort + ":" + offset);
            return ByCategoryAndLanguage(offset);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> SearchAsync(string keyword, int pageSize, int pageNumber, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("search:" + keyword + ":" + pageSize + ":" + pageNumber);
            return Search(keyword, pageNumber);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> AdvancedSearchAsync(AdvancedSearchFilters filters, RequestPriority priority, CancellationToken cancellationToken)
        {
            LastAdvanced = filters;
            Record("advanced:" + filters.Page);
            return Advanced(filters.Page ?? 0);
        }

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetMainCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("main:" + lang);
            return Main();
        }

        public Task<ApiResult<IReadOnlyList<CategoryInfo>>> GetSubCategoriesAsync(string lang, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("sub:" + lang);
            return Sub();
        }

        public Task<ApiResult<IReadOnlyList<CategoryLanguageGroup>>> GetCategoryLanguageGroupsAsync(RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("languages");
            return Languages();
        }
    }

    /// <summary>A delayer the test releases by hand, so debounce behaviour is deterministic.</summary>
    internal sealed class ManualDelayer : IDelayer
    {
        private readonly List<TaskCompletionSource<bool>> _waiting = new List<TaskCompletionSource<bool>>();

        public int Pending
        {
            get
            {
                lock (_waiting)
                {
                    return _waiting.Count;
                }
            }
        }

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_waiting)
            {
                _waiting.Add(tcs);
            }

            return tcs.Task;
        }

        public async Task WaitForPending(int count)
        {
            for (var i = 0; i < 400 && Pending < count; i++)
            {
                await Task.Delay(5);
            }

            Assert.IsTrue(Pending >= count, "expected " + count + " pending delays but there were " + Pending);
        }

        public void ReleaseAll()
        {
            List<TaskCompletionSource<bool>> copy;
            lock (_waiting)
            {
                copy = _waiting.ToList();
                _waiting.Clear();
            }

            foreach (var tcs in copy)
            {
                tcs.TrySetResult(true);
            }
        }
    }

    [TestClass]
    public class BrowseModelsTests
    {
        [TestMethod]
        public void BrowseQuery_LanguageNeedsACategory_AndUnknownSortFallsBack()
        {
            Assert.IsNull(new BrowseQuery(ListingKind.Movies, null, "22").LanguageId);
            Assert.IsNull(new BrowseQuery(ListingKind.Movies, "  ", "22").CategoryId);
            Assert.AreEqual("22", new BrowseQuery(ListingKind.Movies, "57", " 22 ").LanguageId);
            Assert.AreEqual(ListingSort.Desc, new BrowseQuery(ListingKind.Movies, null, null, "made-up").Sort);
            Assert.AreEqual(ListingSort.StarsAsc, new BrowseQuery(ListingKind.Movies, null, null, ListingSort.StarsAsc).Sort);
        }

        [TestMethod]
        public void BrowseQuery_HasValueEquality_AndCopyMethods()
        {
            var a = new BrowseQuery(ListingKind.Series, "57", "22", ListingSort.Asc);
            var b = new BrowseQuery(ListingKind.Series, "57", "22", ListingSort.Asc);
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, a.WithKind(ListingKind.Movies));
            Assert.AreNotEqual(a, a.WithSort(ListingSort.Desc));
            Assert.IsNull(a.WithLanguage(null).LanguageId);
            Assert.AreEqual(ListingKind.Movies, a.WithKind(ListingKind.Movies).Kind);
        }

        [TestMethod]
        public void SortChoices_OnlyOfferValuesWhoseMeaningIsEstablished()
        {
            CollectionAssert.AreEquivalent(
                new[] { ListingSort.Desc, ListingSort.Asc, ListingSort.StarsDesc, ListingSort.StarsAsc },
                SortChoices.Browse.Select(c => c.Value).ToArray());
            foreach (var choice in SortChoices.Browse)
            {
                Assert.IsTrue(ListingSort.IsKnown(choice.Value));
            }

            Assert.IsNull(SortChoices.Advanced[0].Value, "advanced search must default to no sorting");
            Assert.AreEqual(ListingSort.StarsDesc, SortChoices.FindBrowse(ListingSort.StarsDesc).Value);
            Assert.AreEqual(ListingSort.Desc, SortChoices.FindBrowse("title_asc").Value);
        }

        [TestMethod]
        public void AnimeDefaults_AreTheDocumentedObservedIds()
        {
            Assert.AreEqual("57", AnimeDefinition.CategoryId);
            Assert.AreEqual("22", AnimeDefinition.JapaneseLanguageId);
        }

        [TestMethod]
        public void AdvancedFilters_Criteria_Validity_AndPageCopy()
        {
            Assert.IsFalse(new AdvancedSearchFilters().HasCriteria);
            Assert.IsFalse(new AdvancedSearchFilters { Sort = "stars_desc", Page = 2 }.HasCriteria, "sort and page alone are not criteria");
            Assert.IsFalse(new AdvancedSearchFilters { VideoTitle = "  " }.HasCriteria);
            Assert.IsTrue(new AdvancedSearchFilters { VideoTitle = "x" }.HasCriteria);
            Assert.IsTrue(new AdvancedSearchFilters { Type = SearchType.Series }.HasCriteria);
            Assert.IsTrue(new AdvancedSearchFilters { CategoryIds = new[] { "57" } }.HasCriteria);
            Assert.IsTrue(new AdvancedSearchFilters { Star = 7 }.HasCriteria);

            Assert.IsTrue(new AdvancedSearchFilters { YearFrom = 2000, YearTo = 2005 }.IsValid);
            Assert.IsFalse(new AdvancedSearchFilters { YearFrom = 2000 }.IsValid);
            Assert.IsFalse(new AdvancedSearchFilters { YearFrom = 2006, YearTo = 2005 }.IsValid);
            Assert.IsFalse(new AdvancedSearchFilters { Page = -1 }.IsValid);

            var original = new AdvancedSearchFilters { VideoTitle = "Conan", Page = 0, Type = SearchType.Movies, CategoryIds = new[] { "57" } };
            var next = original.WithPage(3);
            Assert.AreEqual(3, next.Page);
            Assert.AreEqual(0, original.Page);
            Assert.AreEqual("Conan", next.VideoTitle);
            Assert.AreEqual(SearchType.Movies, next.Type);
        }
    }

    [TestClass]
    public class ResultsControllerTests
    {
        private static ResultsController Create(Func<PageRequest, Task<ApiResult<IReadOnlyList<VideoSummary>>>> fetch) =>
            new ResultsController((r, ct) => fetch(r));

        [TestMethod]
        public async Task FirstPage_BecomesContent_AndPagingStartsAtTheRightPlaces()
        {
            var requests = new List<PageRequest>();
            var results = Create(r =>
            {
                requests.Add(r);
                return r.PageIndex == 0 ? FakeDiscoverySource.Ok("1", "2", "3") : FakeDiscoverySource.Ok("4");
            });
            Assert.AreEqual(ResultsState.Idle, results.State);

            await results.LoadFirstAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, results.State);
            Assert.IsTrue(results.CanLoadMore);

            Assert.IsTrue(await results.LoadMoreAsync(CancellationToken.None));
            Assert.AreEqual(0, requests[0].PageIndex);
            Assert.AreEqual(1, requests[1].PageIndex);
            Assert.AreEqual(3, requests[1].ItemsLoaded, "offset-based endpoints receive the number of items already shown");
            Assert.AreEqual(4, results.Items.Count);
        }

        [TestMethod]
        public async Task EmptyFirstPage_IsEmpty_NotError_AndNothingMoreIsRequested()
        {
            var calls = 0;
            var results = Create(r =>
            {
                calls++;
                return FakeDiscoverySource.Ok();
            });
            await results.LoadFirstAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Empty, results.State);
            Assert.IsFalse(await results.LoadMoreAsync(CancellationToken.None));
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public async Task FirstPageFailure_IsError_AndRetryRecovers()
        {
            var fail = true;
            var results = Create(r => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok("1"));
            await results.LoadFirstAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Error, results.State);

            fail = false;
            await results.LoadFirstAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, results.State);
        }

        [TestMethod]
        public async Task LoadMore_EndsWhenAPageAddsNothingNew_AndDeduplicatesAcrossPages()
        {
            var results = Create(r => r.PageIndex == 0 ? FakeDiscoverySource.Ok("1", "2") : r.PageIndex == 1 ? FakeDiscoverySource.Ok("2", "3") : FakeDiscoverySource.Ok("2", "3"));
            await results.LoadFirstAsync(CancellationToken.None);
            Assert.IsTrue(await results.LoadMoreAsync(CancellationToken.None));
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, results.Items.Select(i => i.Id).ToArray());
            Assert.IsFalse(await results.LoadMoreAsync(CancellationToken.None));
            Assert.IsFalse(results.CanLoadMore);
        }

        [TestMethod]
        public async Task LoadMoreFailure_KeepsItems_FlagsTheFailure_AndAllowsRetry()
        {
            var fail = true;
            var results = Create(r => r.PageIndex == 0 ? FakeDiscoverySource.Ok("1") : fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok("2"));
            await results.LoadFirstAsync(CancellationToken.None);

            Assert.IsFalse(await results.LoadMoreAsync(CancellationToken.None));
            Assert.IsTrue(results.LoadMoreFailed);
            Assert.IsTrue(results.CanLoadMore);
            Assert.AreEqual(1, results.Items.Count);
            Assert.IsFalse(results.IsLoadingMore);

            fail = false;
            Assert.IsTrue(await results.LoadMoreAsync(CancellationToken.None));
            Assert.IsFalse(results.LoadMoreFailed);
            Assert.AreEqual(2, results.Items.Count);
        }

        [TestMethod]
        public async Task ConcurrentLoadMore_FetchesOnce()
        {
            var gate = new TaskCompletionSource<bool>();
            var fetches = 0;
            var results = Create(async r =>
            {
                if (r.PageIndex > 0)
                {
                    Interlocked.Increment(ref fetches);
                    await gate.Task;
                    return await FakeDiscoverySource.Ok("2");
                }

                return await FakeDiscoverySource.Ok("1");
            });
            await results.LoadFirstAsync(CancellationToken.None);

            var first = results.LoadMoreAsync(CancellationToken.None);
            Assert.IsFalse(await results.LoadMoreAsync(CancellationToken.None));
            Assert.IsTrue(results.IsLoadingMore);
            gate.SetResult(true);
            Assert.IsTrue(await first);
            Assert.AreEqual(1, fetches);
        }

        [TestMethod]
        public async Task ANewerLoad_SupersedesAnOlderOne_SoStaleResultsNeverAppear()
        {
            var gate = new TaskCompletionSource<bool>();
            var call = 0;
            var results = Create(async r =>
            {
                var n = Interlocked.Increment(ref call);
                if (n == 1)
                {
                    await gate.Task;
                    return await FakeDiscoverySource.Ok("old");
                }

                return await FakeDiscoverySource.Ok("new");
            });

            var first = results.LoadFirstAsync(CancellationToken.None);
            await results.LoadFirstAsync(CancellationToken.None);
            gate.SetResult(true);
            await first;

            CollectionAssert.AreEqual(new[] { "new" }, results.Items.Select(i => i.Id).ToArray());
            Assert.AreEqual(ResultsState.Content, results.State);
        }

        [TestMethod]
        public async Task OutdatedCacheFallback_IsFlagged_AndSnapshotIsConsistent()
        {
            var results = Create(r => FakeDiscoverySource.OkFrom(DataOrigin.CacheAfterError, "1"));
            await results.LoadFirstAsync(CancellationToken.None);
            var snapshot = results.Snapshot;
            Assert.IsTrue(snapshot.Outdated);
            Assert.AreEqual(ResultsState.Content, snapshot.State);
            Assert.AreEqual(1, snapshot.Items.Count);
            Assert.AreEqual(ResultsState.Idle, ResultsSnapshot.Idle.State);
        }

        [TestMethod]
        public async Task Changed_IsRaisedForLoadingAndCompletion()
        {
            var results = Create(r => FakeDiscoverySource.Ok("1"));
            var states = new List<ResultsState>();
            results.Changed += (s, e) => states.Add(results.State);
            await results.LoadFirstAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { ResultsState.Loading, ResultsState.Content }, states);
        }
    }

    [TestClass]
    public class BrowseControllerTests
    {
        private static BrowseController Create(FakeDiscoverySource source, BrowseQuery query) => new BrowseController(source, query);

        [TestMethod]
        public async Task NoCategory_UsesTheGenericListing_WithPageBasedPagination()
        {
            var source = new FakeDiscoverySource { Listing = p => p == 0 ? FakeDiscoverySource.Ok("1", "2") : FakeDiscoverySource.Ok("3") };
            var browse = Create(source, new BrowseQuery(ListingKind.Series, null, null, ListingSort.StarsDesc));

            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            await browse.LoadMoreAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "listing:Series:stars_desc:30:0", "listing:Series:stars_desc:30:1" },
                source.Calls.ToArray());
            Assert.AreEqual(3, browse.Results.Items.Count);
        }

        [TestMethod]
        public async Task Category_UsesVideosByCategory_WithOffsetBasedPagination()
        {
            var source = new FakeDiscoverySource { ByCategory = o => o == 0 ? FakeDiscoverySource.Ok("1", "2", "3") : FakeDiscoverySource.Ok("4") };
            var browse = Create(source, new BrowseQuery(ListingKind.Movies, "84"));

            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            await browse.LoadMoreAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "category:84:Movies:desc:0", "category:84:Movies:desc:3" }, source.Calls.ToArray());
        }

        [TestMethod]
        public async Task CategoryAndLanguage_UsesTheCombinedEndpoint_WithOffsets()
        {
            var source = new FakeDiscoverySource { ByCategoryAndLanguage = o => o == 0 ? FakeDiscoverySource.Ok("1", "2") : FakeDiscoverySource.Ok("3") };
            var browse = Create(source, new BrowseQuery(ListingKind.Series, "57", "22", ListingSort.Asc));

            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            await browse.LoadMoreAsync(CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "categoryLanguage:57:22:Series:asc:0", "categoryLanguage:57:22:Series:asc:2" },
                source.Calls.ToArray());
        }

        [TestMethod]
        public async Task ChangingTheQuery_ReloadsFromTheFirstPage_WithTheNewEndpoint()
        {
            var source = new FakeDiscoverySource { ByCategory = o => FakeDiscoverySource.Ok("a"), ByCategoryAndLanguage = o => FakeDiscoverySource.Ok("b") };
            var browse = Create(source, new BrowseQuery(ListingKind.Movies, "57"));
            await browse.ApplyAsync(browse.Query, CancellationToken.None);

            await browse.ApplyAsync(browse.Query.WithLanguage("22"), CancellationToken.None);
            await browse.ApplyAsync(browse.Query.WithKind(ListingKind.Series), CancellationToken.None);

            CollectionAssert.AreEqual(
                new[] { "category:57:Movies:desc:0", "categoryLanguage:57:22:Movies:desc:0", "categoryLanguage:57:22:Series:desc:0" },
                source.Calls.ToArray());
            Assert.AreEqual("b", browse.Results.Items.Single().Id);
        }

        [TestMethod]
        public async Task ApplyingTheSameQuery_DoesNotReload_ButAFailedOneDoes()
        {
            var fail = true;
            var source = new FakeDiscoverySource { Listing = p => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok("1") };
            var browse = Create(source, new BrowseQuery(ListingKind.Movies));

            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            Assert.AreEqual(ResultsState.Error, browse.Results.State);

            fail = false;
            await browse.ApplyAsync(new BrowseQuery(ListingKind.Movies), CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, browse.Results.State);

            await browse.ApplyAsync(new BrowseQuery(ListingKind.Movies), CancellationToken.None);
            Assert.AreEqual(2, source.Calls.Count, "an unchanged, successful query must not hit the network again");
        }

        [TestMethod]
        public async Task Retry_ReloadsTheCurrentQuery()
        {
            var fail = true;
            var source = new FakeDiscoverySource { Listing = p => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok("1") };
            var browse = Create(source, new BrowseQuery(ListingKind.Movies));
            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            fail = false;
            await browse.RetryAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, browse.Results.State);
        }

        [TestMethod]
        public async Task Changed_IsRaisedWhenTheResultsOfTheCurrentQueryChange()
        {
            var source = new FakeDiscoverySource { Listing = p => FakeDiscoverySource.Ok("1") };
            var browse = Create(source, new BrowseQuery(ListingKind.Movies));
            var raised = 0;
            browse.Changed += (s, e) => Interlocked.Increment(ref raised);
            await browse.ApplyAsync(browse.Query, CancellationToken.None);
            Assert.IsTrue(raised >= 2);
        }
    }

    [TestClass]
    public class SearchControllerTests
    {
        private static SearchController Create(FakeDiscoverySource source, IDelayer delayer) =>
            new SearchController(source, delayer, TimeSpan.FromMilliseconds(450));

        [TestMethod]
        public async Task TypingBurst_SendsExactlyOneRequest_ForTheFinalText()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => FakeDiscoverySource.Ok("1") };
            var delayer = new ManualDelayer();
            var search = Create(source, delayer);

            foreach (var text in new[] { "c", "co", "con", "cona", "conan" })
            {
                search.SetText(text);
            }

            await delayer.WaitForPending(5);
            Assert.AreEqual(0, source.Calls.Count, "nothing is sent while the debounce period is running");

            delayer.ReleaseAll();
            await search.WaitForPendingAsync();
            await Task.Delay(50);

            CollectionAssert.AreEqual(new[] { "search:conan:12:0" }, source.Calls.ToArray());
            Assert.AreEqual(ResultsState.Content, search.State);
            Assert.AreEqual("conan", search.ExecutedQuery);
        }

        [TestMethod]
        public async Task Submit_SearchesImmediately_AndCancelsThePendingDebounce()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => FakeDiscoverySource.Ok("1") };
            var delayer = new ManualDelayer();
            var search = Create(source, delayer);

            search.SetText("con");
            await delayer.WaitForPending(1);
            await search.SubmitAsync("Conan", CancellationToken.None);

            delayer.ReleaseAll();
            await search.WaitForPendingAsync();
            await Task.Delay(50);

            CollectionAssert.AreEqual(new[] { "search:Conan:12:0" }, source.Calls.ToArray());
        }

        [TestMethod]
        public async Task ClearingTheText_ReturnsToIdle_AndIgnoresThePendingSearch()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => FakeDiscoverySource.Ok("1") };
            var delayer = new ManualDelayer();
            var search = Create(source, delayer);

            search.SetText("conan");
            await delayer.WaitForPending(1);
            search.SetText("   ");
            delayer.ReleaseAll();
            await search.WaitForPendingAsync();
            await Task.Delay(50);

            Assert.AreEqual(0, source.Calls.Count);
            Assert.AreEqual(ResultsState.Idle, search.State);
            Assert.AreEqual(string.Empty, search.ExecutedQuery);
        }

        [TestMethod]
        public async Task BlankSubmit_ClearsWithoutARequest()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => FakeDiscoverySource.Ok("1") };
            var search = Create(source, new ManualDelayer());
            await search.SubmitAsync("conan", CancellationToken.None);
            await search.SubmitAsync("  ", CancellationToken.None);
            Assert.AreEqual(1, source.Calls.Count);
            Assert.AreEqual(ResultsState.Idle, search.State);
        }

        [TestMethod]
        public async Task SameQueryAgain_IsNotSentTwice_ButSubmitCanForceIt()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => FakeDiscoverySource.Ok("1") };
            var delayer = new ManualDelayer();
            var search = Create(source, delayer);

            search.SetText("conan");
            await delayer.WaitForPending(1);
            delayer.ReleaseAll();
            await search.WaitForPendingAsync();
            await Task.Delay(50);

            search.SetText("Conan");
            await delayer.WaitForPending(1);
            delayer.ReleaseAll();
            await search.WaitForPendingAsync();
            await Task.Delay(50);
            Assert.AreEqual(1, source.Calls.Count, "only the letter case changed");

            await search.SubmitAsync("Conan", CancellationToken.None);
            Assert.AreEqual(2, source.Calls.Count);
        }

        [TestMethod]
        public async Task OlderResults_NeverReplaceNewerOnes()
        {
            var gate = new TaskCompletionSource<bool>();
            var source = new FakeDiscoverySource
            {
                Search = async (k, p) =>
                {
                    if (k == "slow")
                    {
                        await gate.Task;
                        return await FakeDiscoverySource.Ok("old");
                    }

                    return await FakeDiscoverySource.Ok("new");
                },
            };
            var search = Create(source, new ManualDelayer());

            var first = search.SubmitAsync("slow", CancellationToken.None);
            await search.SubmitAsync("fast", CancellationToken.None);
            gate.SetResult(true);
            await first;

            CollectionAssert.AreEqual(new[] { "new" }, search.Snapshot.Items.Select(i => i.Id).ToArray());
            Assert.AreEqual("fast", search.ExecutedQuery);
        }

        [TestMethod]
        public async Task EmptyResults_ErrorAndRetry()
        {
            var fail = true;
            var source = new FakeDiscoverySource { Search = (k, p) => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok() };
            var search = Create(source, new ManualDelayer());

            await search.SubmitAsync("conan", CancellationToken.None);
            Assert.AreEqual(ResultsState.Error, search.State);

            fail = false;
            await search.RetryAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Empty, search.State);
        }

        [TestMethod]
        public async Task LoadMore_UsesZeroBasedPages_AndTheDocumentedPageSize()
        {
            var source = new FakeDiscoverySource { Search = (k, p) => p == 0 ? FakeDiscoverySource.Ok("1", "2") : FakeDiscoverySource.Ok("3") };
            var search = Create(source, new ManualDelayer());
            await search.SubmitAsync("  conan  ", CancellationToken.None);
            Assert.IsTrue(await search.LoadMoreAsync(CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "search:conan:12:0", "search:conan:12:1" }, source.Calls.ToArray());
            Assert.AreEqual(3, search.Snapshot.Items.Count);
        }

        [TestMethod]
        public async Task LoadMore_WithoutASearch_DoesNothing()
        {
            var search = Create(new FakeDiscoverySource(), new ManualDelayer());
            Assert.IsFalse(await search.LoadMoreAsync(CancellationToken.None));
            Assert.AreEqual(ResultsState.Idle, search.State);
            Assert.AreEqual(0, search.Items.Count);
        }
    }

    [TestClass]
    public class AdvancedSearchControllerTests
    {
        [TestMethod]
        public async Task NoCriteriaOrInvalidFilters_AreNotSent()
        {
            var source = new FakeDiscoverySource();
            var advanced = new AdvancedSearchController(source);

            Assert.IsFalse(await advanced.ApplyAsync(new AdvancedSearchFilters(), CancellationToken.None));
            Assert.IsFalse(await advanced.ApplyAsync(new AdvancedSearchFilters { Sort = "stars_desc" }, CancellationToken.None));
            Assert.IsFalse(await advanced.ApplyAsync(new AdvancedSearchFilters { VideoTitle = "x", YearFrom = 2010 }, CancellationToken.None));
            Assert.IsFalse(await advanced.ApplyAsync(new AdvancedSearchFilters { VideoTitle = "x", YearFrom = 2010, YearTo = 2000 }, CancellationToken.None));
            Assert.AreEqual(0, source.Calls.Count);
            Assert.AreEqual(ResultsState.Idle, advanced.State);
        }

        [TestMethod]
        public async Task ValidFilters_AreSent_AndPaginationUsesZeroBasedPages()
        {
            var source = new FakeDiscoverySource { Advanced = p => p == 0 ? FakeDiscoverySource.Ok("1", "2") : FakeDiscoverySource.Ok("3") };
            var advanced = new AdvancedSearchController(source);

            Assert.IsTrue(await advanced.ApplyAsync(new AdvancedSearchFilters { VideoTitle = "Conan", Type = SearchType.Series }, CancellationToken.None));
            Assert.IsTrue(await advanced.LoadMoreAsync(CancellationToken.None));

            CollectionAssert.AreEqual(new[] { "advanced:0", "advanced:1" }, source.Calls.ToArray());
            Assert.AreEqual(3, advanced.Snapshot.Items.Count);
            Assert.AreEqual("Conan", source.LastAdvanced!.VideoTitle);
            Assert.AreEqual(SearchType.Series, source.LastAdvanced.Type);
        }

        [TestMethod]
        public async Task EditingTheFormAfterApplying_DoesNotChangeThatSearchesPagination()
        {
            var source = new FakeDiscoverySource { Advanced = p => FakeDiscoverySource.Ok("a" + p) };
            var advanced = new AdvancedSearchController(source);
            var filters = new AdvancedSearchFilters { VideoTitle = "Conan" };
            await advanced.ApplyAsync(filters, CancellationToken.None);

            filters.VideoTitle = "Something else";
            await advanced.LoadMoreAsync(CancellationToken.None);
            Assert.AreEqual("Conan", source.LastAdvanced!.VideoTitle);
        }

        [TestMethod]
        public async Task ResetReturnsToIdle_AndErrorsCanBeRetried()
        {
            var fail = true;
            var source = new FakeDiscoverySource { Advanced = p => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Ok("1") };
            var advanced = new AdvancedSearchController(source);

            await advanced.ApplyAsync(new AdvancedSearchFilters { VideoTitle = "x" }, CancellationToken.None);
            Assert.AreEqual(ResultsState.Error, advanced.State);

            fail = false;
            await advanced.RetryAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, advanced.State);

            advanced.Reset();
            Assert.AreEqual(ResultsState.Idle, advanced.State);
            Assert.IsFalse(await advanced.LoadMoreAsync(CancellationToken.None));
        }
    }

    [TestClass]
    public class CategoryCatalogTests
    {
        private static CategoryCatalogLoader Loader(FakeDiscoverySource source) => new CategoryCatalogLoader(source);

        [TestMethod]
        public async Task MainAndSubCategories_AreJoinedWithTheirLanguages_AndOrderedByTitle()
        {
            var source = new FakeDiscoverySource
            {
                Main = () => FakeDiscoverySource.Cats("84=Action", "57=Animation", "56=Adventure"),
                Sub = () => FakeDiscoverySource.Cats("113=Slice of Life", "58=Biography"),
                Languages = () => Task.FromResult(new ApiResult<IReadOnlyList<CategoryLanguageGroup>>(
                    new[] { FakeDiscoverySource.LangGroup("57", "22:10:5:Japanese", "7:50:20:English") }, DataOrigin.Network, null)),
            };
            var catalog = await Loader(source).LoadAsync("en", false, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "84", "56", "57", "58", "113" }, catalog.All.Select(c => c.Id).ToArray()); // Action, Adventure, Animation | Biography, Slice of Life
            Assert.IsTrue(catalog.All.Take(3).All(c => c.IsMain));
            Assert.IsTrue(catalog.All.Skip(3).All(c => !c.IsMain));
            Assert.IsTrue(catalog.HasLanguageMetadata);
            Assert.AreEqual(2, catalog.Find("57")!.Languages.Count);
            Assert.AreEqual(0, catalog.Find("84")!.Languages.Count);
            Assert.IsNull(catalog.Find("999"));
            Assert.IsNull(catalog.Find(null));
        }

        [TestMethod]
        public async Task DuplicateIds_AcrossMainAndSub_AppearOnce()
        {
            var source = new FakeDiscoverySource { Main = () => FakeDiscoverySource.Cats("1=A"), Sub = () => FakeDiscoverySource.Cats("1=A", "2=B") };
            var catalog = await Loader(source).LoadAsync("en", false, CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "1", "2" }, catalog.All.Select(c => c.Id).ToArray());
            Assert.IsTrue(catalog.Find("1")!.IsMain);
        }

        [TestMethod]
        public async Task LanguageMetadataFailure_DegradesToNoLanguageFilter()
        {
            var source = new FakeDiscoverySource { Main = () => FakeDiscoverySource.Cats("84=Action"), Languages = () => throw FakeHomeSource.Fail() };
            var catalog = await Loader(source).LoadAsync("en", false, CancellationToken.None);
            Assert.AreEqual(1, catalog.All.Count);
            Assert.IsFalse(catalog.HasLanguageMetadata);
        }

        [TestMethod]
        public async Task OneCategoryListFailing_StillGivesTheOther_BothFailingThrows()
        {
            var partial = new FakeDiscoverySource { Main = () => throw FakeHomeSource.Fail(), Sub = () => FakeDiscoverySource.Cats("58=Biography") };
            Assert.AreEqual(1, (await Loader(partial).LoadAsync("en", false, CancellationToken.None)).All.Count);

            var none = new FakeDiscoverySource { Main = () => throw FakeHomeSource.Fail(), Sub = () => throw FakeHomeSource.Fail() };
            await Assert.ThrowsExceptionAsync<ApiException>(() => Loader(none).LoadAsync("en", false, CancellationToken.None));
        }

        [TestMethod]
        public void LanguagesFor_FiltersEmptyOnes_AndOrdersByPopulation()
        {
            var entry = new CategoryEntry("57", "Animation", null, true, FakeDiscoverySource.LangGroup(
                "57", "22:10:0:Japanese", "7:50:20:English", "9:0:0:Arabic", "11:?:?:French", "14:10:3:Spanish").Languages);

            CollectionAssert.AreEqual(new[] { "7", "22", "14", "11" }, entry.LanguagesFor(ListingKind.Movies).Select(l => l.LanguageId).ToArray());
            CollectionAssert.AreEqual(new[] { "7", "14", "11" }, entry.LanguagesFor(ListingKind.Series).Select(l => l.LanguageId).ToArray());
        }

        [TestMethod]
        public void EntryTitle_PrefersTheRequestedLanguage_AndFallsBack()
        {
            var both = new CategoryEntry("1", "Action", "أكشن", true, new CategoryLanguage[0]);
            Assert.AreEqual("أكشن", both.GetTitle(true));
            Assert.AreEqual("Action", both.GetTitle(false));
            Assert.AreEqual("Action", new CategoryEntry("1", "Action", null, true, new CategoryLanguage[0]).GetTitle(true));
            Assert.AreEqual("9", new CategoryEntry("9", null, null, true, new CategoryLanguage[0]).GetTitle(false));
        }

        [TestMethod]
        public async Task Provider_CachesPerLanguage_SharesInFlightLoads_AndDoesNotRememberFailures()
        {
            var lang = "en";
            var loads = 0;
            var fail = true;
            var gate = new TaskCompletionSource<bool>();
            var source = new FakeDiscoverySource
            {
                Main = async () =>
                {
                    Interlocked.Increment(ref loads);
                    await gate.Task;
                    return fail ? throw FakeHomeSource.Fail() : await FakeDiscoverySource.Cats("84=Action");
                },
                Sub = () => fail ? throw FakeHomeSource.Fail() : FakeDiscoverySource.Cats(),
            };
            var provider = new CategoryCatalogProvider(Loader(source), () => lang, () => false);

            Assert.IsNull(provider.TryGetLoaded());
            var a = provider.GetAsync(CancellationToken.None);
            var b = provider.GetAsync(CancellationToken.None);
            gate.SetResult(true);
            await Assert.ThrowsExceptionAsync<ApiException>(() => a);
            await Assert.ThrowsExceptionAsync<ApiException>(() => b);
            Assert.AreEqual(1, loads, "concurrent callers share one load");

            fail = false;
            var ok = await provider.GetAsync(CancellationToken.None);
            Assert.AreEqual(1, ok.All.Count);
            Assert.AreEqual(2, loads, "a failure is not remembered");

            await provider.GetAsync(CancellationToken.None);
            Assert.AreEqual(2, loads, "a loaded catalog is reused");
            Assert.IsNotNull(provider.TryGetLoaded());

            lang = "ar";
            Assert.IsNull(provider.TryGetLoaded());
            await provider.GetAsync(CancellationToken.None);
            Assert.AreEqual(3, loads, "each language has its own catalog");
        }

        [TestMethod]
        public async Task Provider_CancelledCaller_StopsWaiting_WithoutStoppingTheSharedLoad()
        {
            var gate = new TaskCompletionSource<bool>();
            var source = new FakeDiscoverySource
            {
                Main = async () =>
                {
                    await gate.Task;
                    return await FakeDiscoverySource.Cats("84=Action");
                },
            };
            var provider = new CategoryCatalogProvider(Loader(source), () => "en", () => false);

            using (var cts = new CancellationTokenSource())
            {
                var waiting = provider.GetAsync(cts.Token);
                cts.Cancel();
                OperationCanceledException? caught = null;
                try
                {
                    await waiting;
                }
                catch (OperationCanceledException ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(caught);
            }

            gate.SetResult(true);
            var catalog = await provider.GetAsync(CancellationToken.None);
            Assert.AreEqual(1, catalog.All.Count);
        }
    }
}
