using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;
using Xmana.Core.Caching;
using Xmana.Core.Library;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    [TestClass]
    public class HistoryLoaderTests
    {
        private static readonly DateTime T = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        private static (HistoryLoader Loader, MemoryProgressStore Progress, LocalLibrary Library, FakeDetailsSource Source) Create()
        {
            var progress = new MemoryProgressStore();
            var library = new LocalLibrary(new MemoryLibraryStorage(), new FakeClock());
            var source = new FakeDetailsSource();
            return (new HistoryLoader(progress, library, source), progress, library, source);
        }

        [TestMethod]
        public void LoadLocal_UsesOnlyWhatIsKnown_AndMakesNoRequests()
        {
            var (loader, progress, library, source) = Create();
            library.RememberSnapshot(Vid.Make("1"));
            progress.Save(Vid.Progress("1", 600, 3000, T));
            progress.Save(Vid.Progress("2", 600, 3000, T.AddMinutes(1)));

            var items = loader.LoadLocal();
            CollectionAssert.AreEqual(new[] { "1" }, items.Select(i => i.Video.Id).ToArray());
            Assert.AreEqual(0, source.Calls.Count);
        }

        [TestMethod]
        public async Task MissingTitles_AreFetchedOnce_Remembered_AndListed()
        {
            var (loader, progress, library, source) = Create();
            progress.Save(Vid.Progress("7", 600, 3000, T));
            source.Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "Fetched"), DataOrigin.Network, null));

            var items = await loader.LoadAsync(CancellationToken.None);
            Assert.AreEqual("Fetched", items.Single().Video.EnTitle);
            Assert.AreEqual("Fetched", library.GetSnapshot("7")!.EnTitle);

            await loader.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, source.Calls.Count(c => c == "info:7"), "a remembered title is not fetched again");
        }

        [TestMethod]
        public async Task TitlesThatCannotBeLoaded_AreLeftOut_WithoutFailing()
        {
            var (loader, progress, library, source) = Create();
            library.RememberSnapshot(Vid.Make("1"));
            progress.Save(Vid.Progress("1", 600, 3000, T));
            progress.Save(Vid.Progress("bad", 600, 3000, T.AddMinutes(1)));
            progress.Save(Vid.Progress("gone", 600, 3000, T.AddMinutes(2)));
            source.Info = id => id == "gone"
                ? Task.FromResult(new ApiResult<VideoDetails?>(null, DataOrigin.Network, null))
                : throw FakeHomeSource.Fail();

            var items = await loader.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "1" }, items.Select(i => i.Video.Id).ToArray());
        }

        [TestMethod]
        public async Task HydrationIsBounded()
        {
            var (loader, progress, library, source) = Create();
            for (var i = 0; i < 30; i++)
            {
                progress.Save(Vid.Progress("m" + i, 600, 3000, T.AddMinutes(i)));
            }

            await loader.LoadAsync(CancellationToken.None);
            Assert.AreEqual(HistoryLoader.MaxHydrations, source.Calls.Count(c => c.StartsWith("info:")));
        }

        [TestMethod]
        public async Task Cancellation_Stops_AndRemoveForgetsTheTitle()
        {
            var (loader, progress, library, source) = Create();
            progress.Save(Vid.Progress("1", 600, 3000, T));
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                OperationCanceledException? caught = null;
                try
                {
                    await loader.LoadAsync(cts.Token);
                }
                catch (OperationCanceledException ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(caught);
            }

            loader.Remove("1");
            Assert.IsNull(progress.Get("1"));
        }
    }

    [TestClass]
    public class DirectorySizeTests
    {
        [TestMethod]
        public void SumsFilesRecursively_AndIsZeroForMissingOrBlankPaths()
        {
            var dir = Path.Combine(Path.GetTempPath(), "xmana-size-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(dir, "sub"));
                File.WriteAllBytes(Path.Combine(dir, "a.bin"), new byte[100]);
                File.WriteAllBytes(Path.Combine(dir, "sub", "b.bin"), new byte[250]);
                Assert.AreEqual(350, DirectorySize.Of(dir));
                Assert.AreEqual(0, DirectorySize.Of(Path.Combine(dir, "missing")));
                Assert.AreEqual(0, DirectorySize.Of(null));
                Assert.AreEqual(0, DirectorySize.Of("  "));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [TestMethod]
        public void PlayerPreferences_Reset_RestoresEveryDefault()
        {
            var store = new FakeSettingsStore();
            var prefs = new PlayerPreferences(store);
            prefs.PreferredQualityHeight = 1080;
            prefs.Subtitle = SubtitlePreference.Arabic;
            prefs.AutoplayNext = false;
            prefs.ResumeEnabled = false;
            prefs.SkipIntroEnabled = false;

            prefs.Reset();
            var fresh = new PlayerPreferences(store);
            Assert.IsNull(fresh.PreferredQualityHeight);
            Assert.AreEqual(SubtitlePreference.Unset, fresh.Subtitle);
            Assert.IsTrue(fresh.AutoplayNext);
            Assert.IsTrue(fresh.ResumeEnabled);
            Assert.IsTrue(fresh.SkipIntroEnabled);
        }
    }

    [TestClass]
    public class SessionSettingsTests
    {
        [TestMethod]
        public async Task ResumeSwitchedOff_NeverOffersOrUsesTheSavedPosition()
        {
            var settings = new FakeSettingsStore();
            var prefs = new PlayerPreferences(settings) { ResumeEnabled = false };
            var engine = new FakePlayerEngine();
            var progress = new MemoryProgressStore();
            progress.Save(new PlaybackProgress("192319", null, null, null, TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(3000), DateTime.UtcNow, false));
            var context = new PlaybackContext("192319", "Movie", null, null, null, null, null, null, new VideoSummary[0], null);
            var session = new PlaybackSession(context, new FakePlaybackData(), engine, progress, prefs, new FakeClock(), new RecordingDelayer());

            await session.PrepareAsync(CancellationToken.None);
            Assert.IsFalse(session.CanResume);
            session.Start(PlaybackStartMode.Resume);
            Assert.AreEqual(TimeSpan.Zero, engine.Opens.Single().Start);
        }

        [TestMethod]
        public async Task SkipIntroSwitchedOff_HidesTheControl_EvenWithVerifiedData()
        {
            var settings = new FakeSettingsStore();
            var prefs = new PlayerPreferences(settings) { SkipIntroEnabled = false };
            var engine = new FakePlayerEngine();
            var data = new FakePlaybackData
            {
                Skip = () => Task.FromResult(Xmana.Core.Api.Mapping.PlaybackMapper.ToSkipInfo(Xmana.Core.Json.JsonParser.Parse("{\"start\":[10],\"end\":[90]}"))),
            };
            var context = new PlaybackContext("1", "E", null, "S", 1, 1, null, null, new VideoSummary[0], true);
            var session = new PlaybackSession(context, data, engine, new MemoryProgressStore(), prefs, new FakeClock(), new RecordingDelayer(), null, SkipTimeUnit.Seconds);

            await session.PrepareAsync(CancellationToken.None);
            session.Start(PlaybackStartMode.StartOver);
            engine.RaiseOpened();
            session.Tick(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1500));
            Assert.IsNull(session.ActiveSkip);

            prefs.SkipIntroEnabled = true;
            Assert.IsNotNull(session.ActiveSkip);
        }
    }
}
