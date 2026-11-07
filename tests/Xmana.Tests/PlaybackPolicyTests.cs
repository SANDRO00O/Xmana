using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Json;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    internal static class Q
    {
        /// <summary>Qualities for the given heights; <paramref name="generation"/> changes the (fake, signed) addresses like a refresh would.</summary>
        public static System.Collections.Generic.IReadOnlyList<VideoQuality> WithGeneration(int generation, params int[] heights) =>
            heights.Select(h => new VideoQuality(h + "p", h, "https://cdn.invalid/v" + h + ".mp4?sig=SECRET" + generation)).ToList();

        public static System.Collections.Generic.IReadOnlyList<VideoQuality> Of(params int[] heights) => WithGeneration(1, heights);
    }

    [TestClass]
    public class ProgressPolicyTests
    {
        [DataTestMethod]
        [DataRow(0, 0, false)]
        [DataRow(100, 0, false)]
        [DataRow(1000, 2000, false)]
        [DataRow(1900, 2000, true)]
        [DataRow(1981, 2000, true)]
        [DataRow(1899, 2000, false)]
        [DataRow(2000, 2000, true)]
        [DataRow(50, 60, true)]
        public void Completion_IsNearTheEnd_AndUnknownDurationsAreNeverCompleted(int positionSeconds, int durationSeconds, bool expected)
        {
            Assert.AreEqual(expected, ProgressPolicy.IsCompleted(TimeSpan.FromSeconds(positionSeconds), TimeSpan.FromSeconds(durationSeconds)));
        }

        private static PlaybackProgress P(int pos, int dur, bool done = false) =>
            new PlaybackProgress("1", null, null, null, TimeSpan.FromSeconds(pos), TimeSpan.FromSeconds(dur), DateTime.UtcNow, done);

        [TestMethod]
        public void Resumable_NeedsAMeaningfulPosition_AndNotTheEnd()
        {
            Assert.IsTrue(ProgressPolicy.IsResumable(P(600, 3000)));
            Assert.IsFalse(ProgressPolicy.IsResumable(null));
            Assert.IsFalse(ProgressPolicy.IsResumable(P(10, 3000)), "too early to be worth a prompt");
            Assert.IsFalse(ProgressPolicy.IsResumable(P(2990, 3000)), "effectively at the end");
            Assert.IsFalse(ProgressPolicy.IsResumable(P(600, 3000, done: true)), "completed content is not resumed");
            Assert.IsTrue(ProgressPolicy.IsResumable(P(600, 0)), "an unknown duration cannot rule resuming out");
        }

        [TestMethod]
        public void ResumePosition_RewindsSlightly_AndNeverGoesNegative()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(597), ProgressPolicy.ResumePosition(P(600, 3000)));
            Assert.AreEqual(TimeSpan.Zero, ProgressPolicy.ResumePosition(P(2, 3000)));
        }
    }

    [TestClass]
    public class QualitySelectorTests
    {
        [TestMethod]
        public void Choose_PrefersTheStoredHeight_ThenTheBestBelow_ThenTheLowest()
        {
            var all = Q.Of(240, 360, 480, 720, 1080);
            Assert.AreEqual(1080, QualitySelector.Choose(all, 1080)!.Height);
            Assert.AreEqual(480, QualitySelector.Choose(Q.Of(240, 480, 1080), 720)!.Height, "720 missing: best quality below it");
            Assert.AreEqual(240, QualitySelector.Choose(Q.Of(240, 360), 100)!.Height, "everything is above the preference: the lowest");
        }

        [TestMethod]
        public void Choose_WithoutAPreference_UsesASafeDefault_AndNeverAssumesFiveQualities()
        {
            Assert.AreEqual(720, QualitySelector.Choose(Q.Of(240, 360, 480, 720, 1080), null)!.Height);
            Assert.AreEqual(360, QualitySelector.Choose(Q.Of(240, 360, 1080), null)!.Height);
            Assert.AreEqual(1080, QualitySelector.Choose(Q.Of(1080), null)!.Height, "a single, higher quality is still chosen");
            Assert.IsNull(QualitySelector.Choose(Q.Of(), null));
        }

        [TestMethod]
        public void Fallback_TriesLowerQualitiesNearestFirst_ThenHigher_SkippingFailedOnes()
        {
            var all = Q.Of(240, 360, 480, 720, 1080);
            var failed = new System.Collections.Generic.HashSet<int> { 720 };
            Assert.AreEqual(480, QualitySelector.Fallback(all, all.First(q => q.Height == 720), failed)!.Height);

            failed.Add(480);
            failed.Add(360);
            failed.Add(240);
            Assert.AreEqual(1080, QualitySelector.Fallback(all, all.First(q => q.Height == 720), failed)!.Height);

            failed.Add(1080);
            Assert.IsNull(QualitySelector.Fallback(all, all.First(q => q.Height == 720), failed));
        }
    }

    [TestClass]
    public class SeekAcceleratorTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void RepeatedPresses_Accelerate_UpToSixtySeconds()
        {
            var seek = new SeekAccelerator();
            var steps = Enumerable.Range(0, 10).Select(i => (int)seek.Step(1, T0.AddMilliseconds(i * 200)).TotalSeconds).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 10, 20, 20, 30, 30, 30, 60, 60, 60 }, steps);
        }

        [TestMethod]
        public void BackwardSteps_AreNegative()
        {
            var seek = new SeekAccelerator();
            Assert.AreEqual(TimeSpan.FromSeconds(-10), seek.Step(-1, T0));
        }

        [TestMethod]
        public void ADirectionChange_OrAPause_StartsOver()
        {
            var seek = new SeekAccelerator();
            seek.Step(1, T0);
            seek.Step(1, T0.AddMilliseconds(100));
            seek.Step(1, T0.AddMilliseconds(200));
            Assert.AreEqual(TimeSpan.FromSeconds(-10), seek.Step(-1, T0.AddMilliseconds(300)));
            Assert.AreEqual(TimeSpan.FromSeconds(10), seek.Step(1, T0.AddMilliseconds(400)));

            seek.Step(1, T0.AddMilliseconds(500));
            seek.Step(1, T0.AddMilliseconds(600));
            Assert.AreEqual(TimeSpan.FromSeconds(10), seek.Step(1, T0.AddSeconds(5)), "a long pause resets the acceleration");
        }

        [TestMethod]
        public void Reset_AndInvalidDirection()
        {
            var seek = new SeekAccelerator();
            seek.Step(1, T0);
            seek.Step(1, T0.AddMilliseconds(100));
            seek.Step(1, T0.AddMilliseconds(200));
            seek.Reset();
            Assert.AreEqual(TimeSpan.FromSeconds(10), seek.Step(1, T0.AddMilliseconds(300)));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => seek.Step(0, T0));
        }
    }

    [TestClass]
    public class AutoNextControllerTests
    {
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(1500);

        [TestMethod]
        public void Hidden_WithoutANextEpisode_OrBeforeTheWindow()
        {
            var auto = new AutoNextController();
            auto.Update(TimeSpan.FromSeconds(1490), Duration, false, true);
            Assert.AreEqual(AutoNextPhase.Hidden, auto.Phase);

            auto.Update(TimeSpan.FromSeconds(600), Duration, true, true);
            Assert.AreEqual(AutoNextPhase.Hidden, auto.Phase);

            auto.Update(TimeSpan.FromSeconds(1490), TimeSpan.Zero, true, true);
            Assert.AreEqual(AutoNextPhase.Hidden, auto.Phase, "an unknown duration never offers anything");
        }

        [TestMethod]
        public void OfferAppearsInTheLastSeconds_WithTheCountdownOnlyAtTheVeryEnd()
        {
            var auto = new AutoNextController();
            Assert.IsTrue(auto.Update(TimeSpan.FromSeconds(1480), Duration, true, true));
            Assert.AreEqual(AutoNextPhase.Offering, auto.Phase);
            Assert.AreEqual(20, auto.SecondsRemaining);
            Assert.IsFalse(auto.ShowCountdown);

            auto.Update(TimeSpan.FromSeconds(1492), Duration, true, true);
            Assert.AreEqual(8, auto.SecondsRemaining);
            Assert.IsTrue(auto.ShowCountdown);
            Assert.IsFalse(auto.ShouldAdvance, "nothing advances before the end of the media");
        }

        [TestMethod]
        public void ItAdvancesOnce_WhenTheEpisodeReachesItsEnd()
        {
            var auto = new AutoNextController();
            auto.Update(TimeSpan.FromSeconds(1499), Duration, true, true);
            Assert.IsFalse(auto.ShouldAdvance);
            auto.Update(Duration, Duration, true, true);
            Assert.IsTrue(auto.ShouldAdvance);
            auto.Update(Duration, Duration, true, true);
            Assert.IsFalse(auto.ShouldAdvance, "only once");
        }

        [TestMethod]
        public void AutoplayOff_IsManualOnly_NeverAdvancesOrCountsDown()
        {
            var auto = new AutoNextController();
            auto.Update(TimeSpan.FromSeconds(1495), Duration, true, false);
            Assert.AreEqual(AutoNextPhase.Offering, auto.Phase);
            Assert.IsNull(auto.SecondsRemaining);
            Assert.IsFalse(auto.ShowCountdown);

            auto.Update(Duration, Duration, true, false);
            Assert.IsFalse(auto.ShouldAdvance);
            auto.OnEnded(true, false);
            Assert.IsFalse(auto.ShouldAdvance);
            Assert.AreEqual(AutoNextPhase.Offering, auto.Phase);
        }

        [TestMethod]
        public void Cancel_HidesTheOffer_UntilTheViewerSeeksOutAndBack()
        {
            var auto = new AutoNextController();
            auto.Update(TimeSpan.FromSeconds(1490), Duration, true, true);
            auto.Cancel();
            Assert.AreEqual(AutoNextPhase.Hidden, auto.Phase);

            auto.Update(TimeSpan.FromSeconds(1495), Duration, true, true);
            auto.Update(Duration, Duration, true, true);
            Assert.AreEqual(AutoNextPhase.Hidden, auto.Phase);
            Assert.IsFalse(auto.ShouldAdvance, "a cancelled offer must never advance");

            auto.Update(TimeSpan.FromSeconds(100), Duration, true, true);
            auto.Update(TimeSpan.FromSeconds(1490), Duration, true, true);
            Assert.AreEqual(AutoNextPhase.Offering, auto.Phase);
        }

        [TestMethod]
        public void OnEnded_Advances_OnlyWithANextEpisodeAndAutoplay_AndNotWhenCancelled()
        {
            var auto = new AutoNextController();
            auto.OnEnded(true, true);
            Assert.IsTrue(auto.ShouldAdvance);

            var none = new AutoNextController();
            none.OnEnded(false, true);
            Assert.IsFalse(none.ShouldAdvance);

            var cancelled = new AutoNextController();
            cancelled.Cancel();
            cancelled.OnEnded(true, true);
            Assert.IsFalse(cancelled.ShouldAdvance);
        }

        [TestMethod]
        public void Reset_ForgetsEverything()
        {
            var auto = new AutoNextController();
            auto.Cancel();
            auto.Reset();
            Assert.IsFalse(auto.IsCancelled);
            auto.Update(TimeSpan.FromSeconds(1495), Duration, true, true);
            Assert.AreEqual(AutoNextPhase.Offering, auto.Phase);
        }
    }

    [TestClass]
    public class SkipControllerTests
    {
        private static SkipInfo Info(string json) => PlaybackMapper.ToSkipInfo(JsonParser.Parse(json));

        [TestMethod]
        public void WhileTheUnitIsUnverified_NothingIsEverShown()
        {
            Assert.AreEqual(SkipTimeUnit.Unknown, SkipController.VerifiedUnit, "the unit must stay unknown until a populated response is verified");
            Assert.IsNull(SkipController.Active(Info("{\"start\":[10],\"end\":[90]}"), true, SkipTimeUnit.Unknown, TimeSpan.FromSeconds(20)));
        }

        [TestMethod]
        public void Seconds_AndMilliseconds_AreConvertedExplicitly()
        {
            var seconds = SkipController.Active(Info("{\"start\":[10],\"end\":[90]}"), true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(20));
            Assert.AreEqual(TimeSpan.FromSeconds(90), seconds!.End);
            var millis = SkipController.Active(Info("{\"start\":[10000],\"end\":[90000]}"), true, SkipTimeUnit.Milliseconds, TimeSpan.FromSeconds(20));
            Assert.AreEqual(TimeSpan.FromSeconds(90), millis!.End);
            Assert.AreEqual(TimeSpan.FromSeconds(10), millis.Start);
        }

        [TestMethod]
        public void OnlyInsideTheRange_AndNotInTheLastSecond()
        {
            var info = Info("{\"start\":[10],\"end\":[90]}");
            Assert.IsNull(SkipController.Active(info, true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(5)));
            Assert.IsNotNull(SkipController.Active(info, true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(10)));
            Assert.IsNull(SkipController.Active(info, true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(89.5)));
            Assert.IsNull(SkipController.Active(info, true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(120)));
        }

        [TestMethod]
        public void TheServerFlag_AndMissingData_HideTheControl()
        {
            var info = Info("{\"start\":[10],\"end\":[90]}");
            Assert.IsNull(SkipController.Active(info, false, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(20)));
            Assert.IsNotNull(SkipController.Active(info, null, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(20)));
            Assert.IsNull(SkipController.Active(null, true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(20)));
            Assert.IsNull(SkipController.Active(Info("{\"start\":[],\"end\":[]}"), true, SkipTimeUnit.Seconds, TimeSpan.FromSeconds(20)));
        }

        [TestMethod]
        public void ConvertWithAnUnknownUnit_Throws_SoNothingIsGuessed()
        {
            Assert.ThrowsException<InvalidOperationException>(() => SkipController.Convert(10, SkipTimeUnit.Unknown));
        }
    }

    [TestClass]
    public class PlayerPreferencesTests
    {
        [TestMethod]
        public void Defaults_AreNeutral()
        {
            var prefs = new PlayerPreferences(new FakeSettingsStore());
            Assert.IsNull(prefs.PreferredQualityHeight);
            Assert.AreEqual(SubtitlePreference.Unset, prefs.Subtitle);
            Assert.IsTrue(prefs.AutoplayNext);
        }

        [TestMethod]
        public void Values_RoundTrip_AndSurviveANewInstance()
        {
            var store = new FakeSettingsStore();
            var prefs = new PlayerPreferences(store);
            prefs.PreferredQualityHeight = 480;
            prefs.Subtitle = SubtitlePreference.Arabic;
            prefs.AutoplayNext = false;

            var again = new PlayerPreferences(store);
            Assert.AreEqual(480, again.PreferredQualityHeight);
            Assert.AreEqual(SubtitlePreference.Arabic, again.Subtitle);
            Assert.IsFalse(again.AutoplayNext);

            again.Subtitle = SubtitlePreference.Off;
            Assert.AreEqual(SubtitlePreference.Off, new PlayerPreferences(store).Subtitle);
        }

        [TestMethod]
        public void CorruptValues_FallBackToDefaults_AndInvalidQualityIsIgnored()
        {
            var store = new FakeSettingsStore();
            store.Values[PlayerPreferences.QualityKey] = "abc";
            store.Values[PlayerPreferences.SubtitleKey] = "klingon";
            var prefs = new PlayerPreferences(store);
            Assert.IsNull(prefs.PreferredQualityHeight);
            Assert.AreEqual(SubtitlePreference.Unset, prefs.Subtitle);

            prefs.PreferredQualityHeight = 0;
            Assert.IsNull(prefs.PreferredQualityHeight);
        }

        [TestMethod]
        public void SubtitleConversions_MapBothWays()
        {
            Assert.IsNull(PlayerPreferences.ToLanguage(SubtitlePreference.Off));
            Assert.IsNull(PlayerPreferences.ToLanguage(SubtitlePreference.Unset));
            Assert.AreEqual(SubtitleLanguage.Spanish, PlayerPreferences.ToLanguage(SubtitlePreference.Spanish));
            Assert.AreEqual(SubtitlePreference.Off, PlayerPreferences.From(null));
            Assert.AreEqual(SubtitlePreference.English, PlayerPreferences.From(SubtitleLanguage.English));
        }
    }

    [TestClass]
    public class ProgressStoreTests
    {
        private string _dir = string.Empty;

        [TestInitialize]
        public void Init() => _dir = Path.Combine(Path.GetTempPath(), "xmana-progress-" + Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        private static PlaybackProgress P(string id, int pos, DateTime played, bool done = false) =>
            new PlaybackProgress(id, "S", 2, 5, TimeSpan.FromSeconds(pos), TimeSpan.FromSeconds(1500), played, done);

        [TestMethod]
        public void File_RoundTrips_AllFields_AcrossInstances()
        {
            var path = Path.Combine(_dir, "progress.json");
            var when = new DateTime(2026, 10, 1, 9, 30, 0, DateTimeKind.Utc);
            new FileProgressStore(path).Save(P("1119122", 600, when, done: true));

            var read = new FileProgressStore(path).Get("1119122")!;
            Assert.AreEqual("S", read.SeriesId);
            Assert.AreEqual(2, read.Season);
            Assert.AreEqual(5, read.Episode);
            Assert.AreEqual(TimeSpan.FromSeconds(600), read.Position);
            Assert.AreEqual(TimeSpan.FromSeconds(1500), read.Duration);
            Assert.AreEqual(when, read.LastPlayedUtc);
            Assert.IsTrue(read.Completed);
            Assert.IsNull(new FileProgressStore(path).Get("nope"));
        }

        [TestMethod]
        public void File_NeverContainsAStreamAddress()
        {
            var path = Path.Combine(_dir, "progress.json");
            new FileProgressStore(path).Save(P("1", 100, DateTime.UtcNow));
            var text = File.ReadAllText(path);
            Assert.IsFalse(text.Contains("http"));
            Assert.IsFalse(text.Contains("sig="));
        }

        [TestMethod]
        public void File_CorruptOrWrongVersion_IsTreatedAsEmpty_AndReplacedOnTheNextSave()
        {
            var path = Path.Combine(_dir, "progress.json");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(path, "{ not json");
            var store = new FileProgressStore(path);
            Assert.IsNull(store.Get("1"));

            store.Save(P("1", 100, DateTime.UtcNow));
            Assert.IsNotNull(new FileProgressStore(path).Get("1"));

            File.WriteAllText(path, "{\"v\":99,\"items\":[{\"id\":\"1\"}]}");
            Assert.IsNull(new FileProgressStore(path).Get("1"));
        }

        [TestMethod]
        public void File_InvalidEntries_AreSkipped_ValidOnesKept()
        {
            var path = Path.Combine(_dir, "progress.json");
            Directory.CreateDirectory(_dir);
            File.WriteAllText(path, "{\"v\":1,\"items\":[{\"id\":\"bad\",\"pos\":-5,\"dur\":10,\"played\":\"x\"},{\"pos\":1},5,{\"id\":\"ok\",\"pos\":30,\"dur\":100,\"played\":\"2026-10-01T09:30:00.0000000Z\"}]}");
            var store = new FileProgressStore(path);
            Assert.IsNull(store.Get("bad"));
            Assert.AreEqual(TimeSpan.FromSeconds(30), store.Get("ok")!.Position);
        }

        [TestMethod]
        public void RecentIsNewestFirst_RemoveWorks_AndTheFileIsBounded()
        {
            var path = Path.Combine(_dir, "progress.json");
            var store = new FileProgressStore(path);
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < 520; i++)
            {
                store.Save(P("v" + i, 60, start.AddMinutes(i)));
            }

            Assert.IsNull(store.Get("v0"), "the oldest entries are dropped beyond the cap");
            Assert.IsNotNull(store.Get("v519"));
            CollectionAssert.AreEqual(new[] { "v519", "v518", "v517" }, store.Recent(3).Select(p => p.VideoId).ToArray());

            store.Remove("v519");
            Assert.IsNull(store.Get("v519"));
            Assert.AreEqual("v518", new FileProgressStore(path).Recent(1).Single().VideoId);
        }

        [TestMethod]
        public void Memory_BehavesTheSame()
        {
            var store = new MemoryProgressStore();
            var when = DateTime.UtcNow;
            store.Save(P("a", 60, when));
            store.Save(P("b", 60, when.AddMinutes(1)));
            store.Save(P("a", 90, when.AddMinutes(2)));
            CollectionAssert.AreEqual(new[] { "a", "b" }, store.Recent(5).Select(p => p.VideoId).ToArray());
            Assert.AreEqual(TimeSpan.FromSeconds(90), store.Get("a")!.Position);
            store.Remove("a");
            Assert.IsNull(store.Get("a"));
        }
    }
}
