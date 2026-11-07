using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Home;
using Xmana.Core.Json;
using Xmana.Core.Library;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    internal static class Vid
    {
        public static VideoSummary Make(string id, string title = "T", string extra = "") =>
            VideoMapper.ToSummary(JsonParser.Parse("{\"nb\":\"" + id + "\",\"en_title\":\"" + title + "\",\"ar_title\":\"ع" + id + "\",\"year\":\"2020\",\"kind\":\"1\",\"stars\":\"7.5\",\"img\":\"https://img.invalid/" + id + ".jpg\",\"imgThumb\":\"https://img.invalid/t" + id + ".jpg\"" + extra + "}"))!;

        public static PlaybackProgress Progress(string id, int pos, int dur, DateTime played, string? series = null, bool done = false) =>
            new PlaybackProgress(id, series, series == null ? (int?)null : 1, series == null ? (int?)null : 2, TimeSpan.FromSeconds(pos), TimeSpan.FromSeconds(dur), played, done);
    }

    [TestClass]
    public class LibraryItemTests
    {
        [TestMethod]
        public void Snapshot_KeepsPlainFields_AndNeverTheSignedAddresses()
        {
            var video = Vid.Make("5", "Conan", ",\"imgObjUrl\":\"https://signed.invalid/a?sig=SECRET\",\"imgThumbObjUrl\":\"https://signed.invalid/b?sig=SECRET\",\"rootSeries\":\"9\",\"season\":\"2\",\"episodeNummer\":\"4\"");
            var back = LibraryItem.From(video, DateTime.UtcNow).ToSummary();

            Assert.AreEqual("5", back.Id);
            Assert.AreEqual("Conan", back.EnTitle);
            Assert.AreEqual("ع5", back.ArTitle);
            Assert.AreEqual("2020", back.Year);
            Assert.AreEqual(7.5, back.Stars);
            Assert.AreEqual("https://img.invalid/5.jpg", back.ImageUrl);
            Assert.AreEqual("9", back.RootSeriesId);
            Assert.AreEqual(2, back.Season);
            Assert.AreEqual(4, back.EpisodeNumber);
            Assert.IsNull(back.ImageObjUrl);
            Assert.IsNull(back.ThumbObjUrl);
            Assert.IsFalse(back.Raw.ToString().Contains("SECRET"));
        }

        [TestMethod]
        public void SameContent_IgnoresTheSeenTime()
        {
            var video = Vid.Make("5");
            var a = LibraryItem.From(video, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            var b = LibraryItem.From(video, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            Assert.IsTrue(a.SameContent(b));
            Assert.IsFalse(a.SameContent(LibraryItem.From(Vid.Make("5", "Other title"), DateTime.UtcNow)));
        }
    }

    [TestClass]
    public class LocalLibraryTests
    {
        private static (LocalLibrary Library, MemoryLibraryStorage Storage, FakeClock Clock) Create(MemoryLibraryStorage? storage = null)
        {
            var clock = new FakeClock();
            var store = storage ?? new MemoryLibraryStorage();
            return (new LocalLibrary(store, clock), store, clock);
        }

        [TestMethod]
        public void ToggleFavorite_AddsAndRemoves_AndReportsTheNewState()
        {
            var (library, _, _) = Create();
            var video = Vid.Make("1");
            Assert.IsFalse(library.IsFavorite("1"));
            Assert.IsTrue(library.ToggleFavorite(video));
            Assert.IsTrue(library.IsFavorite("1"));
            Assert.AreEqual(1, library.Favorites().Count);
            Assert.IsFalse(library.ToggleFavorite(video));
            Assert.IsFalse(library.IsFavorite("1"));
            Assert.AreEqual(0, library.Favorites().Count);
        }

        [TestMethod]
        public void FavoritesAndWatchlist_AreIndependent_AndNewestFirst()
        {
            var (library, _, clock) = Create();
            library.ToggleFavorite(Vid.Make("1"));
            clock.Advance(TimeSpan.FromMinutes(1));
            library.ToggleFavorite(Vid.Make("2"));
            clock.Advance(TimeSpan.FromMinutes(1));
            library.ToggleWatchlist(Vid.Make("3"));

            CollectionAssert.AreEqual(new[] { "2", "1" }, library.Favorites().Select(v => v.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "3" }, library.Watchlist().Select(v => v.Id).ToArray());
            Assert.IsFalse(library.IsWatchlisted("1"));
            Assert.IsTrue(library.IsWatchlisted("3"));
        }

        [TestMethod]
        public void Everything_SurvivesARestart()
        {
            var storage = new MemoryLibraryStorage();
            var (first, _, _) = Create(storage);
            first.ToggleFavorite(Vid.Make("1", "Fav"));
            first.ToggleWatchlist(Vid.Make("2", "Later"));
            first.AddSearch("conan");

            var (second, _, _) = Create(storage);
            Assert.AreEqual("Fav", second.Favorites().Single().EnTitle);
            Assert.AreEqual("Later", second.Watchlist().Single().EnTitle);
            CollectionAssert.AreEqual(new[] { "conan" }, second.RecentSearches().ToArray());
        }

        [TestMethod]
        public void TheStoredFile_NeverContainsASignedAddress()
        {
            var (library, storage, _) = Create();
            library.ToggleFavorite(Vid.Make("1", "T", ",\"imgObjUrl\":\"https://signed.invalid/a?sig=SECRET\""));
            Assert.IsFalse(storage.Content!.Contains("SECRET"));
            Assert.IsFalse(storage.Content.Contains("ObjUrl"));
        }

        [TestMethod]
        public void Changed_IsRaisedForRealChanges_NotForAnUnchangedSnapshot()
        {
            var (library, storage, _) = Create();
            var raised = 0;
            library.Changed += (s, e) => raised++;

            library.RememberSnapshot(Vid.Make("1"));
            Assert.AreEqual(1, raised);
            var writes = storage.Writes;

            library.RememberSnapshot(Vid.Make("1"));
            Assert.AreEqual(1, raised, "identical content must not notify");
            Assert.AreEqual(writes, storage.Writes, "identical content must not rewrite the file");

            library.RememberSnapshot(Vid.Make("1", "Changed title"));
            Assert.AreEqual(2, raised);
            Assert.AreEqual("Changed title", library.GetSnapshot("1")!.EnTitle);
            Assert.IsNull(library.GetSnapshot("nope"));
        }

        [TestMethod]
        public void Snapshots_AreBounded_ButListedTitlesAreKept()
        {
            var (library, _, clock) = Create();
            library.ToggleFavorite(Vid.Make("keep"));
            for (var i = 0; i < LocalLibrary.MaxSnapshots + 30; i++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                library.RememberSnapshot(Vid.Make("s" + i));
            }

            Assert.IsNotNull(library.GetSnapshot("keep"), "a favorite's snapshot is never pruned");
            Assert.IsNull(library.GetSnapshot("s0"), "the oldest unreferenced snapshots go first");
            Assert.IsNotNull(library.GetSnapshot("s" + (LocalLibrary.MaxSnapshots + 29)));
        }

        [TestMethod]
        public void Lists_AreBounded()
        {
            var (library, _, clock) = Create();
            for (var i = 0; i < LocalLibrary.MaxListSize + 10; i++)
            {
                clock.Advance(TimeSpan.FromSeconds(1));
                library.ToggleFavorite(Vid.Make("f" + i));
            }

            Assert.AreEqual(LocalLibrary.MaxListSize, library.Favorites().Count);
            Assert.AreEqual("f" + (LocalLibrary.MaxListSize + 9), library.Favorites().First().Id);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("{ not json")]
        [DataRow("{\"v\":99,\"favorites\":[{\"id\":\"1\"}]}")]
        [DataRow("[]")]
        public void CorruptOrForeignFiles_AreTreatedAsEmpty(string? content)
        {
            var storage = new MemoryLibraryStorage();
            storage.Write(content ?? string.Empty);
            var (library, _, _) = Create(storage);
            Assert.AreEqual(0, library.Favorites().Count);
            Assert.AreEqual(0, library.RecentSearches().Count);

            library.ToggleFavorite(Vid.Make("1"));
            Assert.AreEqual(1, Create(storage).Library.Favorites().Count, "the next save replaces the bad file");
        }

        [TestMethod]
        public void ListEntriesWithoutASnapshot_AreDropped_AndBadItemsSkipped()
        {
            var storage = new MemoryLibraryStorage();
            storage.Write("{\"v\":1,\"favorites\":[{\"id\":\"ghost\",\"added\":\"2026-01-01T00:00:00.0000000Z\"},{\"id\":\"1\",\"added\":\"2026-01-02T00:00:00.0000000Z\"}],\"watchlist\":[],"
                + "\"items\":[{\"id\":\"1\",\"en\":\"One\",\"seen\":\"2026-01-02T00:00:00.0000000Z\"},{\"en\":\"no id\"},5],\"searches\":[\"a\",\"A\",\"  \",5]}");
            var (library, _, _) = Create(storage);
            CollectionAssert.AreEqual(new[] { "1" }, library.Favorites().Select(v => v.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "a", "5" }, library.RecentSearches().ToArray(), "case-insensitive duplicates and blanks are dropped");
        }

        [TestMethod]
        public void SearchHistory_IsNormalised_Deduplicated_NewestFirst_AndBounded()
        {
            var (library, _, _) = Create();
            library.AddSearch("  detective   conan ");
            library.AddSearch("one piece");
            library.AddSearch("DETECTIVE CONAN");
            CollectionAssert.AreEqual(new[] { "DETECTIVE CONAN", "one piece" }, library.RecentSearches().ToArray());

            library.AddSearch("   ");
            library.AddSearch(null);
            library.AddSearch(new string('x', LocalLibrary.MaxSearchLength + 1));
            Assert.AreEqual(2, library.RecentSearches().Count);

            for (var i = 0; i < LocalLibrary.MaxSearches + 5; i++)
            {
                library.AddSearch("term" + i);
            }

            Assert.AreEqual(8, library.RecentSearches().Count, "the default view shows eight");
            Assert.AreEqual(LocalLibrary.MaxSearches, library.RecentSearches(100).Count);
            Assert.AreEqual("term" + (LocalLibrary.MaxSearches + 4), library.RecentSearches().First());
        }

        [TestMethod]
        public void SearchHistory_RemoveAndClear()
        {
            var (library, storage, _) = Create();
            library.AddSearch("a");
            library.AddSearch("b");
            library.RemoveSearch("A");
            CollectionAssert.AreEqual(new[] { "b" }, library.RecentSearches().ToArray());
            var writes = storage.Writes;
            library.RemoveSearch("missing");
            Assert.AreEqual(writes, storage.Writes);

            library.ClearSearches();
            Assert.AreEqual(0, library.RecentSearches().Count);
        }

        [TestMethod]
        public void ClearAll_RemovesEverythingAndTheFile()
        {
            var (library, storage, _) = Create();
            library.ToggleFavorite(Vid.Make("1"));
            library.ToggleWatchlist(Vid.Make("2"));
            library.AddSearch("x");
            var raised = 0;
            library.Changed += (s, e) => raised++;

            library.ClearAll();
            Assert.AreEqual(0, library.Favorites().Count);
            Assert.AreEqual(0, library.Watchlist().Count);
            Assert.AreEqual(0, library.RecentSearches().Count);
            Assert.IsNull(library.GetSnapshot("1"));
            Assert.IsNull(storage.Content);
            Assert.AreEqual(1, raised);
        }
    }

    [TestClass]
    public class FileLibraryStorageTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init() => _dir = Path.Combine(Path.GetTempPath(), "xmana-lib-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [TestMethod]
        public void RoundTrips_Atomically_AndDeletes()
        {
            var path = Path.Combine(_dir, "library.json");
            var storage = new FileLibraryStorage(path);
            Assert.IsNull(storage.Read());

            storage.Write("{\"a\":1}");
            storage.Write("{\"a\":2}");
            Assert.AreEqual("{\"a\":2}", storage.Read());
            Assert.AreEqual(0, Directory.GetFiles(_dir, "*.tmp").Length, "no temporary file is left behind");

            storage.Delete();
            Assert.IsNull(storage.Read());
            storage.Delete(); // deleting twice is fine
        }

        [TestMethod]
        public void TheLibrary_WorksOnARealFile()
        {
            var path = Path.Combine(_dir, "library.json");
            new LocalLibrary(new FileLibraryStorage(path)).ToggleFavorite(Vid.Make("1", "On disk"));
            Assert.AreEqual("On disk", new LocalLibrary(new FileLibraryStorage(path)).Favorites().Single().EnTitle);
            Assert.IsFalse(File.ReadAllText(path).Contains("http://"));
        }
    }

    [TestClass]
    public class ContinueWatchingTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private static VideoSummary? Known(string id) => id.StartsWith("x") ? null : Vid.Make(id);

        [TestMethod]
        public void OnlyUnfinishedMeaningfulProgress_IsOffered_NewestFirst()
        {
            var items = ContinueWatching.Build(
                new[]
                {
                    Vid.Progress("old", 600, 3000, T.AddHours(-5)),
                    Vid.Progress("new", 900, 3000, T.AddHours(-1)),
                    Vid.Progress("early", 5, 3000, T),
                    Vid.Progress("done", 2990, 3000, T),
                    Vid.Progress("flagged", 600, 3000, T, done: true),
                },
                Known);
            CollectionAssert.AreEqual(new[] { "new", "old" }, items.Select(i => i.Video.Id).ToArray());
            Assert.AreEqual(0.3, items[0].Fraction, 0.0001);
        }

        [TestMethod]
        public void ForASeries_OnlyTheMostRecentEpisodeIsOffered()
        {
            var items = ContinueWatching.Build(
                new[]
                {
                    Vid.Progress("e1", 600, 1500, T.AddDays(-3), series: "S"),
                    Vid.Progress("e2", 700, 1500, T.AddDays(-1), series: "S"),
                    Vid.Progress("movie", 600, 3000, T.AddDays(-2)),
                },
                Known);
            CollectionAssert.AreEqual(new[] { "e2", "movie" }, items.Select(i => i.Video.Id).ToArray());
        }

        [TestMethod]
        public void AFinishedNewerEpisode_HidesTheSeriesOlderUnfinishedOne()
        {
            var items = ContinueWatching.Build(
                new[]
                {
                    Vid.Progress("e1", 600, 1500, T.AddDays(-3), series: "S"),
                    Vid.Progress("e2", 1495, 1500, T.AddDays(-1), series: "S", done: true),
                },
                Known);
            Assert.AreEqual(0, items.Count, "the viewer moved past e1; offering it again would be wrong");
        }

        [TestMethod]
        public void UnknownTitles_AreSkipped_AndTheListIsCapped()
        {
            var many = Enumerable.Range(0, 30).Select(i => Vid.Progress("v" + i, 600, 3000, T.AddMinutes(-i))).ToList();
            many.Add(Vid.Progress("x1", 600, 3000, T));
            var items = ContinueWatching.Build(many, Known, 5);
            Assert.AreEqual(5, items.Count);
            Assert.IsFalse(items.Any(i => i.Video.Id == "x1"));
        }

        [TestMethod]
        public void Fraction_IsSafe()
        {
            Assert.AreEqual(1, new ProgressItem(Vid.Make("1"), Vid.Progress("1", 10, 100, T, done: true)).Fraction);
            Assert.AreEqual(0, new ProgressItem(Vid.Make("1"), Vid.Progress("1", 10, 0, T)).Fraction);
            Assert.AreEqual(1, new ProgressItem(Vid.Make("1"), Vid.Progress("1", 500, 100, T)).Fraction);
        }

        [TestMethod]
        public void History_ListsEverythingKnown_NewestFirst_IncludingFinished()
        {
            var items = WatchHistory.Build(
                new[]
                {
                    Vid.Progress("a", 600, 3000, T.AddDays(-2)),
                    Vid.Progress("b", 2990, 3000, T, done: true),
                    Vid.Progress("x9", 600, 3000, T.AddDays(-1)),
                },
                Known);
            CollectionAssert.AreEqual(new[] { "b", "a" }, items.Select(i => i.Video.Id).ToArray());
        }
    }

    [TestClass]
    public class StorageFormatTests
    {
        [DataTestMethod]
        [DataRow(0L, "0 B")]
        [DataRow(-5L, "0 B")]
        [DataRow(512L, "512 B")]
        [DataRow(1024L, "1.0 KB")]
        [DataRow(1536L, "1.5 KB")]
        [DataRow(13L * 1024 * 1024 + 400 * 1024, "13.4 MB")]
        [DataRow(3L * 1024 * 1024 * 1024, "3.0 GB")]
        public void Bytes_AreReadable(long bytes, string expected)
        {
            Assert.AreEqual(expected, StorageFormat.Bytes(bytes));
        }
    }

    [TestClass]
    public class FaqReaderTests
    {
        private static System.Collections.Generic.IReadOnlyList<FaqEntry> Entries(string json) =>
            ((JArray)JsonParser.Parse(json)).OfType<JObject>().Select(o => new FaqEntry(o)).ToList();

        [TestMethod]
        public void SimpleQuestionAnswerObjects_AreRead()
        {
            var items = FaqReader.Read(Entries("[{\"id\":1,\"question\":\"How?\",\"answer\":\"Like this.\"}]"), false);
            Assert.AreEqual("How?", items.Single().Question);
            Assert.AreEqual("Like this.", items.Single().Answer);
        }

        [TestMethod]
        public void LanguageVariants_PreferTheInterfaceLanguage()
        {
            var json = "[{\"arQuestion\":\"س\",\"enQuestion\":\"Q\",\"arAnswer\":\"ج\",\"enAnswer\":\"A\"}]";
            var arabic = FaqReader.Read(Entries(json), true).Single();
            Assert.AreEqual("س", arabic.Question);
            Assert.AreEqual("ج", arabic.Answer);
            var english = FaqReader.Read(Entries(json), false).Single();
            Assert.AreEqual("Q", english.Question);
            Assert.AreEqual("A", english.Answer);

            var underscored = FaqReader.Read(Entries("[{\"question_ar\":\"س\",\"question_en\":\"Q\"}]"), true).Single();
            Assert.AreEqual("س", underscored.Question);
            Assert.IsNull(underscored.Answer);
        }

        [TestMethod]
        public void EntriesWithoutAReadableQuestion_AreSkipped_AndNothingIsInvented()
        {
            Assert.AreEqual(0, FaqReader.Read(Entries("[{\"foo\":\"bar\"},{\"question\":null},{\"question\":5}]").Skip(0), false).Count(i => i.Question == "bar"));
            Assert.AreEqual(1, FaqReader.Read(Entries("[{\"foo\":\"bar\"},{\"Question\":\"Q?\"}]"), false).Count);
            Assert.AreEqual(0, FaqReader.Read(Entries("[]"), false).Count);
            Assert.AreEqual(0, FaqReader.Read(null!, false).Count);
        }
    }

    [TestClass]
    public class ProgressStoreClearAndPreferencesTests
    {
        [TestMethod]
        public void Clear_ForgetsEverything_InBothStores()
        {
            var memory = new MemoryProgressStore();
            memory.Save(Vid.Progress("1", 60, 100, DateTime.UtcNow));
            memory.Clear();
            Assert.AreEqual(0, memory.Recent(5).Count);

            var path = Path.Combine(Path.GetTempPath(), "xmana-clear-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var file = new FileProgressStore(path);
                file.Save(Vid.Progress("1", 60, 100, DateTime.UtcNow));
                Assert.IsTrue(File.Exists(path));
                file.Clear();
                Assert.AreEqual(0, file.Recent(5).Count);
                Assert.IsFalse(File.Exists(path));
                file.Save(Vid.Progress("2", 60, 100, DateTime.UtcNow));
                Assert.AreEqual("2", new FileProgressStore(path).Recent(5).Single().VideoId);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [TestMethod]
        public void ResumeAndSkipToggles_DefaultOn_AndPersist()
        {
            var store = new FakeSettingsStore();
            var prefs = new PlayerPreferences(store);
            Assert.IsTrue(prefs.ResumeEnabled);
            Assert.IsTrue(prefs.SkipIntroEnabled);
            prefs.ResumeEnabled = false;
            prefs.SkipIntroEnabled = false;
            Assert.IsFalse(new PlayerPreferences(store).ResumeEnabled);
            Assert.IsFalse(new PlayerPreferences(store).SkipIntroEnabled);
        }

        [TestMethod]
        public void ClearQuality_ReturnsToAutomatic()
        {
            var prefs = new PlayerPreferences(new FakeSettingsStore());
            prefs.PreferredQualityHeight = 480;
            prefs.ClearQuality();
            Assert.IsNull(prefs.PreferredQualityHeight);
        }
    }

    [TestClass]
    public class HomeContinueWatchingTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private static ProgressItem Item(string id, int pos) => new ProgressItem(Vid.Make(id), Vid.Progress(id, pos, 3000, T));

        [TestMethod]
        public async Task ContinueWatching_IsTheFirstRail_WithItsProgress_EvenWhenTheNetworkFails()
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
            var home = new HomeController(source, () => "en", new RecordingDelayer(), null, () => new[] { Item("1", 600), Item("2", 1500) });
            await home.LoadAsync(CancellationToken.None);

            Assert.AreEqual(HomeState.Content, home.State, "local content keeps Home useful offline");
            var rail = home.Sections.Single();
            Assert.AreEqual(HomeSectionKind.ContinueWatching, rail.Kind);
            CollectionAssert.AreEqual(new[] { "1", "2" }, rail.Items.Select(v => v.Id).ToArray());
            Assert.AreEqual(0.2, rail.Progress!["1"], 0.0001);
            Assert.AreEqual(0.5, rail.Progress["2"], 0.0001);
            Assert.IsFalse(rail.CanLoadMore);
        }

        [TestMethod]
        public async Task ContinueWatching_SitsBeforeTheOtherRails_AndIsHiddenWhenEmpty()
        {
            var source = new FakeHomeSource { Featured = () => FakeHomeSource.Ok(Xmana.Core.Caching.DataOrigin.Network, "f1") };
            var items = new[] { Item("1", 600) };
            var home = new HomeController(source, () => "en", new RecordingDelayer(), null, () => items);
            await home.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "continueWatching", "featuredMovies" }, home.Sections.Select(s => s.Id).ToArray());

            var none = new HomeController(source, () => "en", new RecordingDelayer(), null, () => new ProgressItem[0]);
            await none.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "featuredMovies" }, none.Sections.Select(s => s.Id).ToArray());
        }

        [TestMethod]
        public async Task RefreshLocal_PicksUpNewProgress_WithoutAnyRequest()
        {
            var current = new System.Collections.Generic.List<ProgressItem>();
            var source = new FakeHomeSource();
            var home = new HomeController(source, () => "en", new RecordingDelayer(), null, () => current.ToList());
            await home.LoadAsync(CancellationToken.None);
            Assert.AreEqual(0, home.Sections.Count);
            var calls = source.Calls.Count;
            var raised = 0;
            home.Changed += (s, e) => raised++;

            current.Add(Item("7", 900));
            home.RefreshLocal();
            Assert.AreEqual("continueWatching", home.Sections.Single().Id);
            Assert.AreEqual(calls, source.Calls.Count, "a local refresh must not touch the network");
            Assert.AreEqual(1, raised);

            current.Clear();
            home.RefreshLocal();
            Assert.AreEqual(0, home.Sections.Count);
        }
    }
}
