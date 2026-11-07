using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Json;

namespace Xmana.Tests
{
    [TestClass]
    public class CacheSanitizerTests
    {
        [TestMethod]
        public void SignedUrlFields_AreRemovedEverywhere_ButInputIsUntouched()
        {
            var original = JsonParser.Parse(Json.Video);
            var cached = JsonParser.Parse(CacheSanitizer.ToCacheJson(original));

            Assert.IsNull(JsonRead.Get(cached, "imgObjUrl"));
            Assert.IsNull(JsonRead.Get(cached, "imgMediumThumbObjUrl"));
            Assert.AreEqual("1119122", JsonRead.Text(JsonRead.Get(cached, "nb")));
            Assert.IsNotNull(JsonRead.Get(cached, "img"));
            Assert.IsNotNull(JsonRead.Get(original, "imgObjUrl"), "the in-memory copy must keep its data");
            Assert.IsFalse(CacheSanitizer.ToCacheJson(original).Contains("SECRET"));
        }

        [TestMethod]
        public void NestedObjectsAndArrays_AreSanitised()
        {
            var json = "{\"info\":[{\"nb\":\"1\",\"imgThumbObjUrl\":\"https://s.invalid/x?sig=SECRET\",\"deep\":{\"imgObjUrl\":\"SECRET\",\"keep\":1}}]}";
            var text = CacheSanitizer.ToCacheJson(JsonParser.Parse(json));
            Assert.IsFalse(text.Contains("SECRET"));
            Assert.IsTrue(text.Contains("\"keep\":1"));
        }
    }

    [TestClass]
    public class ResponseCacheStoreTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init()
        {
            _dir = Path.Combine(Path.GetTempPath(), "xmana-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private static CacheEntry Entry(string key, string json = "[1]") => new CacheEntry(key, json, new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc));

        [TestMethod]
        public async Task Memory_AddUpdateRemoveClear()
        {
            var cache = new MemoryResponseCache();
            await cache.SetAsync(Entry("a", "[1]"), CancellationToken.None);
            Assert.AreEqual("[1]", (await cache.GetAsync("a", CancellationToken.None))!.Json);

            await cache.SetAsync(Entry("a", "[2]"), CancellationToken.None);
            Assert.AreEqual("[2]", (await cache.GetAsync("a", CancellationToken.None))!.Json);

            await cache.RemoveAsync("a", CancellationToken.None);
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));

            await cache.SetAsync(Entry("b"), CancellationToken.None);
            await cache.ClearAsync(CancellationToken.None);
            Assert.AreEqual(0, cache.Count);
        }

        [TestMethod]
        public async Task Memory_EvictsOldestWhenOverCapacity()
        {
            var cache = new MemoryResponseCache(8);
            for (var i = 0; i < 9; i++)
            {
                await cache.SetAsync(new CacheEntry("k" + i, "[]", new DateTime(2026, 1, 1, 0, i, 0, DateTimeKind.Utc)), CancellationToken.None);
            }

            Assert.IsTrue(cache.Count <= 8);
            Assert.IsNull(await cache.GetAsync("k0", CancellationToken.None), "oldest entry should have been dropped");
            Assert.IsNotNull(await cache.GetAsync("k8", CancellationToken.None));
        }

        [TestMethod]
        public async Task File_RoundTrips_AndKeepsTheStoredTime()
        {
            var cache = new FileResponseCache(_dir);
            await cache.SetAsync(Entry("allVideoInfo/id/1", "{\"nb\":\"1\"}"), CancellationToken.None);

            var read = await new FileResponseCache(_dir).GetAsync("allVideoInfo/id/1", CancellationToken.None);
            Assert.IsNotNull(read);
            Assert.AreEqual("{\"nb\":\"1\"}", read!.Json);
            Assert.AreEqual(new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc), read.StoredUtc);
            Assert.AreEqual(DateTimeKind.Utc, read.StoredUtc.Kind);
        }

        [TestMethod]
        public async Task File_UnknownKey_IsAMiss()
        {
            Assert.IsNull(await new FileResponseCache(_dir).GetAsync("nope", CancellationToken.None));
        }

        [TestMethod]
        public async Task File_Update_Remove_Clear()
        {
            var cache = new FileResponseCache(_dir);
            await cache.SetAsync(Entry("a", "[1]"), CancellationToken.None);
            await cache.SetAsync(Entry("a", "[2]"), CancellationToken.None);
            Assert.AreEqual("[2]", (await cache.GetAsync("a", CancellationToken.None))!.Json);

            await cache.RemoveAsync("a", CancellationToken.None);
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));

            await cache.SetAsync(Entry("b"), CancellationToken.None);
            await cache.SetAsync(Entry("c"), CancellationToken.None);
            await cache.ClearAsync(CancellationToken.None);
            Assert.AreEqual(0, Directory.GetFiles(_dir).Length);
        }

        [TestMethod]
        public async Task File_CorruptedFile_IsDeleted_AndTreatedAsMiss()
        {
            var cache = new FileResponseCache(_dir);
            await cache.SetAsync(Entry("a"), CancellationToken.None);
            var file = Directory.GetFiles(_dir).Single();

            File.WriteAllText(file, "{ this is not json");
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));
            Assert.IsFalse(File.Exists(file), "corrupt file should be removed");

            await cache.SetAsync(Entry("a", "[9]"), CancellationToken.None);
            Assert.AreEqual("[9]", (await cache.GetAsync("a", CancellationToken.None))!.Json);
        }

        [TestMethod]
        public async Task File_WrongVersionOrKeyMismatch_IsTreatedAsMiss()
        {
            var cache = new FileResponseCache(_dir);
            await cache.SetAsync(Entry("a"), CancellationToken.None);
            var file = Directory.GetFiles(_dir).Single();

            var envelope = (JObject)JsonParser.Parse(File.ReadAllText(file));
            envelope["v"] = 999;
            File.WriteAllText(file, envelope.ToString());
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));

            await cache.SetAsync(Entry("a"), CancellationToken.None);
            envelope = (JObject)JsonParser.Parse(File.ReadAllText(file));
            envelope["key"] = "someone-else";
            File.WriteAllText(file, envelope.ToString());
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));
        }

        [TestMethod]
        public async Task File_MissingFieldsInEnvelope_IsTreatedAsMiss()
        {
            var cache = new FileResponseCache(_dir);
            await cache.SetAsync(Entry("a"), CancellationToken.None);
            File.WriteAllText(Directory.GetFiles(_dir).Single(), "{\"v\":1}");
            Assert.IsNull(await cache.GetAsync("a", CancellationToken.None));
        }

        [TestMethod]
        public async Task File_ExceedingBudget_IsPrunedOldestFirst()
        {
            var cache = new FileResponseCache(_dir, 2000);
            var big = new string('x', 300);
            for (var i = 0; i < 40; i++)
            {
                await cache.SetAsync(new CacheEntry("k" + i, "\"" + big + "\"", DateTime.UtcNow), CancellationToken.None);
                await Task.Delay(2);
            }

            var total = new DirectoryInfo(_dir).GetFiles("*.json").Sum(f => f.Length);
            Assert.IsTrue(total <= 2000 + 800, "directory should stay near its budget but was " + total);
            Assert.IsNotNull(await cache.GetAsync("k39", CancellationToken.None), "newest entry must survive pruning");
        }

        [TestMethod]
        public async Task Tiered_PromotesDiskHitsIntoMemory_AndWritesBoth()
        {
            var memory = new MemoryResponseCache();
            var disk = new FileResponseCache(_dir);
            var tiered = new TieredResponseCache(memory, disk);

            await disk.SetAsync(Entry("a", "[1]"), CancellationToken.None);
            Assert.IsNull(await memory.GetAsync("a", CancellationToken.None));
            Assert.AreEqual("[1]", (await tiered.GetAsync("a", CancellationToken.None))!.Json);
            Assert.IsNotNull(await memory.GetAsync("a", CancellationToken.None));

            await tiered.SetAsync(Entry("b", "[2]"), CancellationToken.None);
            Assert.IsNotNull(await memory.GetAsync("b", CancellationToken.None));
            Assert.IsNotNull(await disk.GetAsync("b", CancellationToken.None));

            await tiered.RemoveAsync("b", CancellationToken.None);
            Assert.IsNull(await memory.GetAsync("b", CancellationToken.None));
            Assert.IsNull(await disk.GetAsync("b", CancellationToken.None));

            await tiered.ClearAsync(CancellationToken.None);
            Assert.IsNull(await disk.GetAsync("a", CancellationToken.None));
        }
    }

    [TestClass]
    public class CachedJsonSourceTests
    {
        private const string Key = "allVideoInfo/id/1";

        private sealed class Fixture
        {
            public Fixture()
            {
                Clock = new FakeClock();
                Cache = new MemoryResponseCache();
                Source = new CachedJsonSource(Cache, CachePolicy.Default, Clock, new ListLogger());
            }

            public FakeClock Clock { get; }

            public MemoryResponseCache Cache { get; }

            public CachedJsonSource Source { get; }

            public int Fetches { get; set; }

            public Func<CancellationToken, Task<JToken>> Fetch(string json) => t =>
            {
                Fetches++;
                return Task.FromResult(JsonParser.Parse(json));
            };

            public Func<CancellationToken, Task<JToken>> Failing() => t =>
            {
                Fetches++;
                throw new ApiException(ApiErrorKind.Network, "down", null, "x");
            };

            public Task<CachedJson> Get(CacheKind kind, Func<CancellationToken, Task<JToken>> fetch) =>
                Source.GetAsync(Key, kind, fetch, CancellationToken.None);
        }

        [TestMethod]
        public async Task Miss_FetchesFromNetwork_AndStores()
        {
            var f = new Fixture();
            var result = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            Assert.AreEqual(DataOrigin.Network, result.Origin);
            Assert.AreEqual(1, f.Fetches);
            Assert.IsNotNull(await f.Cache.GetAsync(Key, CancellationToken.None));
        }

        [TestMethod]
        public async Task Fresh_IsServedFromCache_WithoutAnyRequest()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));

            f.Clock.Advance(TimeSpan.FromMinutes(30));
            var again = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"2\"}"));

            Assert.AreEqual(DataOrigin.CacheFresh, again.Origin);
            Assert.AreEqual("1", JsonRead.Text(JsonRead.Get(again.Token, "nb")));
            Assert.AreEqual(1, f.Fetches);
            Assert.IsNotNull(again.StoredUtc);
        }

        [TestMethod]
        public async Task Stale_IsServedImmediately_AndRefreshedInTheBackground()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"old\"}"));

            f.Clock.Advance(TimeSpan.FromHours(7)); // past the 6 h TTL, inside the stale window
            var refreshed = new TaskCompletionSource<string>();
            f.Source.BackgroundRefreshed += (s, key) => refreshed.TrySetResult(key);

            var stale = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"new\"}"));
            Assert.AreEqual(DataOrigin.CacheStale, stale.Origin);
            Assert.AreEqual("old", JsonRead.Text(JsonRead.Get(stale.Token, "v")));

            await f.Source.WaitForRefreshesAsync();
            Assert.AreEqual(Key, await refreshed.Task);
            Assert.AreEqual(2, f.Fetches);

            var next = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"unused\"}"));
            Assert.AreEqual(DataOrigin.CacheFresh, next.Origin);
            Assert.AreEqual("new", JsonRead.Text(JsonRead.Get(next.Token, "v")));
        }

        [TestMethod]
        public async Task Stale_ConcurrentReads_StartOnlyOneRefresh()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            f.Clock.Advance(TimeSpan.FromHours(7));

            var gate = new TaskCompletionSource<bool>();
            Func<CancellationToken, Task<JToken>> slow = async t =>
            {
                f.Fetches++;
                await gate.Task;
                return JsonParser.Parse("{\"nb\":\"1\"}");
            };

            var a = await f.Get(CacheKind.VideoMetadata, slow);
            var b = await f.Get(CacheKind.VideoMetadata, slow);
            var c = await f.Get(CacheKind.VideoMetadata, slow);
            Assert.AreEqual(DataOrigin.CacheStale, a.Origin);
            Assert.AreEqual(DataOrigin.CacheStale, b.Origin);
            Assert.AreEqual(DataOrigin.CacheStale, c.Origin);

            gate.SetResult(true);
            await f.Source.WaitForRefreshesAsync();
            Assert.AreEqual(2, f.Fetches, "one initial fetch + exactly one refresh");
        }

        [TestMethod]
        public async Task Stale_BackgroundRefreshFailure_IsSwallowed_AndCacheIsKept()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            f.Clock.Advance(TimeSpan.FromHours(7));

            var stale = await f.Get(CacheKind.VideoMetadata, f.Failing());
            Assert.AreEqual(DataOrigin.CacheStale, stale.Origin);
            await f.Source.WaitForRefreshesAsync(); // must not throw
            Assert.IsNotNull(await f.Cache.GetAsync(Key, CancellationToken.None));
        }

        [TestMethod]
        public async Task Expired_RefetchesFromNetwork()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"old\"}"));
            f.Clock.Advance(TimeSpan.FromDays(5)); // beyond ttl + stale window (6 h + 72 h)

            var result = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"new\"}"));
            Assert.AreEqual(DataOrigin.Network, result.Origin);
            Assert.AreEqual("new", JsonRead.Text(JsonRead.Get(result.Token, "v")));
        }

        [TestMethod]
        public async Task Expired_NetworkFails_FallsBackToOlderCopyWithinTheFallbackLimit()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\",\"v\":\"old\"}"));
            f.Clock.Advance(TimeSpan.FromDays(5));

            var result = await f.Get(CacheKind.VideoMetadata, f.Failing());
            Assert.AreEqual(DataOrigin.CacheAfterError, result.Origin);
            Assert.AreEqual("old", JsonRead.Text(JsonRead.Get(result.Token, "v")));
        }

        [TestMethod]
        public async Task Expired_NetworkFails_BeyondFallbackLimit_ThrowsInsteadOfTrustingForever()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            f.Clock.Advance(TimeSpan.FromDays(60)); // VideoMetadata error fallback is 14 days

            await Assert.ThrowsExceptionAsync<ApiException>(() => f.Get(CacheKind.VideoMetadata, f.Failing()));
        }

        [TestMethod]
        public async Task Miss_NetworkFails_Throws()
        {
            var f = new Fixture();
            await Assert.ThrowsExceptionAsync<ApiException>(() => f.Get(CacheKind.VideoMetadata, f.Failing()));
        }

        [TestMethod]
        public async Task Cancellation_IsNotConvertedIntoACacheFallback()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            f.Clock.Advance(TimeSpan.FromDays(5));

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                Func<CancellationToken, Task<JToken>> cancelled = t => throw new OperationCanceledException(t);
                await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => f.Source.GetAsync(Key, CacheKind.VideoMetadata, cancelled, cts.Token));
            }
        }

        [TestMethod]
        public async Task KindNone_NeverReadsOrWritesTheCache()
        {
            var f = new Fixture();
            var first = await f.Get(CacheKind.None, f.Fetch("{\"a\":1}"));
            var second = await f.Get(CacheKind.None, f.Fetch("{\"a\":2}"));

            Assert.AreEqual(DataOrigin.Network, first.Origin);
            Assert.AreEqual(DataOrigin.Network, second.Origin);
            Assert.AreEqual(2, f.Fetches);
            Assert.AreEqual(0, f.Cache.Count);
        }

        [TestMethod]
        public async Task ClockMovingBackwards_DoesNotTrustTheEntryForever()
        {
            var f = new Fixture();
            await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            f.Clock.Advance(TimeSpan.FromDays(-30));

            var result = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"2\"}"));
            Assert.AreEqual(DataOrigin.CacheFresh, result.Origin); // treated as age zero, not as "infinitely fresh"

            f.Clock.Advance(TimeSpan.FromDays(31));
            var later = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"3\"}"));
            Assert.AreNotEqual(DataOrigin.CacheFresh, later.Origin);
        }

        [TestMethod]
        public async Task CorruptCachedJson_IsDiscarded_AndRefetched()
        {
            var f = new Fixture();
            await f.Cache.SetAsync(new CacheEntry(Key, "{ broken", f.Clock.UtcNow), CancellationToken.None);

            var result = await f.Get(CacheKind.VideoMetadata, f.Fetch("{\"nb\":\"1\"}"));
            Assert.AreEqual(DataOrigin.Network, result.Origin);
            Assert.AreEqual(1, f.Fetches);
        }

        [TestMethod]
        public async Task StoredCopy_NeverContainsSignedUrls_ButTheReturnedDataDoes()
        {
            var f = new Fixture();
            var result = await f.Get(CacheKind.VideoMetadata, f.Fetch(Json.Video));

            Assert.IsNotNull(JsonRead.Get(result.Token, "imgObjUrl"));
            var stored = await f.Cache.GetAsync(Key, CancellationToken.None);
            Assert.IsFalse(stored!.Json.Contains("SECRET"));
            Assert.IsFalse(stored.Json.Contains("ObjUrl"));
        }

        [TestMethod]
        public void Policy_EveryKindHasSaneLifetimes()
        {
            foreach (CacheKind kind in Enum.GetValues(typeof(CacheKind)))
            {
                if (kind == CacheKind.None)
                {
                    continue;
                }

                var rule = CachePolicy.Default.For(kind);
                Assert.IsTrue(rule.Ttl > TimeSpan.Zero, kind.ToString());
                Assert.IsTrue(rule.ErrorFallback >= rule.Ttl + rule.StaleWindow, kind.ToString());
                Assert.IsTrue(rule.ErrorFallback <= TimeSpan.FromDays(31), kind + " must not be trusted for more than a month");
            }
        }
    }
}
