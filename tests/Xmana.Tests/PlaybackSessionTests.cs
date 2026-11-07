using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    internal sealed class FakePlayerEngine : IPlayerEngine
    {
        public List<(PlaybackSource Source, TimeSpan Start)> Opens { get; } = new List<(PlaybackSource, TimeSpan)>();

        public List<string> Calls { get; } = new List<string>();

        public bool OpenImmediately { get; set; }

        public SubtitleLanguage? LastSubtitle { get; private set; }

        public TimeSpan Position { get; set; }

        public TimeSpan Duration { get; set; }

        public event EventHandler? Opened;

        public event EventHandler? Ended;

        public event EventHandler<EngineFailureEventArgs>? Failed;

        public event EventHandler<bool>? BufferingChanged;

        public void Open(PlaybackSource source, TimeSpan start)
        {
            lock (Calls)
            {
                Opens.Add((source, start));
                Calls.Add("open");
            }

            if (OpenImmediately)
            {
                Opened?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Play() => Record("play");

        public void Pause() => Record("pause");

        public void Seek(TimeSpan position) => Record("seek:" + (int)position.TotalSeconds);

        public void SelectSubtitle(SubtitleLanguage? language)
        {
            LastSubtitle = language;
            Record("subtitle:" + (language?.ToString() ?? "off"));
        }

        public void Stop() => Record("stop");

        public void RaiseOpened() => Opened?.Invoke(this, EventArgs.Empty);

        public void RaiseEnded() => Ended?.Invoke(this, EventArgs.Empty);

        public void RaiseFailed(PlayerFailure failure) => Failed?.Invoke(this, new EngineFailureEventArgs(failure));

        public void RaiseBuffering(bool buffering) => BufferingChanged?.Invoke(this, buffering);

        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }
    }

    internal sealed class FakePlaybackData : IPlaybackDataSource
    {
        public int QualityCalls { get; private set; }

        public Func<int, Task<IReadOnlyList<VideoQuality>>> Qualities { get; set; } = n => Task.FromResult(Q.WithGeneration(n, 240, 480, 720, 1080));

        public Func<Task<TranslationInfo>> Translations { get; set; } = () => Task.FromResult(Subs());

        public Func<Task<SkipInfo>> Skip { get; set; } = () => Task.FromResult(PlaybackMapper.ToSkipInfo(JsonParser.Parse("{\"start\":[],\"end\":[]}")));

        public static TranslationInfo Subs(string ar = "https://subs.invalid/ar.srt?sig=SECRET", string en = "https://subs.invalid/en.srt?sig=SECRET", string sp = "") =>
            PlaybackMapper.ToTranslations(JsonParser.Parse("{\"arTranslationFile\":\"" + ar + "\",\"enTranslationFile\":\"" + en + "\",\"spTranslationFile\":\"" + sp + "\"}"));

        public Task<IReadOnlyList<VideoQuality>> GetQualitiesAsync(string videoId, CancellationToken cancellationToken)
        {
            int call;
            lock (this)
            {
                call = ++QualityCalls;
            }

            return Qualities(call);
        }

        public Task<TranslationInfo> GetTranslationsAsync(string videoId, CancellationToken cancellationToken) => Translations();

        public Task<SkipInfo> GetSkipInfoAsync(string videoId, CancellationToken cancellationToken) => Skip();
    }

    [TestClass]
    public class PlaybackSessionTests
    {
        private sealed class Harness
        {
            public Harness(PlaybackContext? context = null, SkipTimeUnit? skipUnit = null, FakeSettingsStore? settings = null)
            {
                Engine = new FakePlayerEngine();
                Data = new FakePlaybackData();
                Progress = new MemoryProgressStore();
                Clock = new FakeClock();
                Logger = new ListLogger();
                Settings = settings ?? new FakeSettingsStore();
                Preferences = new PlayerPreferences(Settings);
                Session = new PlaybackSession(context ?? Movie(), Data, Engine, Progress, Preferences, Clock, new RecordingDelayer(), Logger, skipUnit);
            }

            public FakePlayerEngine Engine { get; }

            public FakePlaybackData Data { get; }

            public MemoryProgressStore Progress { get; }

            public FakeClock Clock { get; }

            public ListLogger Logger { get; }

            public FakeSettingsStore Settings { get; }

            public PlayerPreferences Preferences { get; }

            public PlaybackSession Session { get; }

            /// <summary>Prepares, starts and lets the engine open: the session is Playing at <paramref name="start"/>.</summary>
            public async Task PlayAsync(PlaybackStartMode mode = PlaybackStartMode.StartOver)
            {
                await Session.PrepareAsync(CancellationToken.None);
                Session.Start(mode);
                Engine.RaiseOpened();
            }
        }

        private static PlaybackContext Movie() => new PlaybackContext("192319", "Movie", null, null, null, null, null, null, new VideoSummary[0], null);

        private static PlaybackContext Episode(bool withNext = true, bool? intro = null) =>
            new PlaybackContext(
                "1119122",
                "Episode",
                null,
                "1119119",
                1,
                2,
                FakeHomeSource.Videos("1119121").Single(),
                withNext ? FakeHomeSource.Videos("1119123").Single() : null,
                FakeHomeSource.Videos("1119121", "1119122", "1119123"),
                intro);

        private static async Task WaitFor(Func<bool> condition, string what)
        {
            for (var i = 0; i < 400 && !condition(); i++)
            {
                await Task.Delay(5);
            }

            Assert.IsTrue(condition(), "timed out waiting for " + what);
        }

        // ---- preparation ------------------------------------------------------------------------

        [TestMethod]
        public async Task Prepare_ChoosesTheDefaultQuality_AndIsReady()
        {
            var h = new Harness();
            await h.Session.PrepareAsync(CancellationToken.None);

            Assert.AreEqual(PlayerState.Ready, h.Session.State);
            Assert.AreEqual(720, h.Session.CurrentQuality!.Height);
            Assert.AreEqual(4, h.Session.Qualities.Count);
            Assert.AreEqual(0, h.Engine.Opens.Count, "preparing never starts the engine");
        }

        [TestMethod]
        public async Task Prepare_UsesTheStoredQualityPreference()
        {
            var settings = new FakeSettingsStore();
            new PlayerPreferences(settings).PreferredQualityHeight = 480;
            var h = new Harness(settings: settings);
            await h.Session.PrepareAsync(CancellationToken.None);
            Assert.AreEqual(480, h.Session.CurrentQuality!.Height);
        }

        [TestMethod]
        public async Task NoQualities_IsNoSource_AndQualityFailureIsANetworkFailure()
        {
            var empty = new Harness();
            empty.Data.Qualities = n => Task.FromResult(Q.Of());
            await empty.Session.PrepareAsync(CancellationToken.None);
            Assert.AreEqual(PlayerState.Failed, empty.Session.State);
            Assert.AreEqual(PlayerFailure.NoSource, empty.Session.Failure);

            var failing = new Harness();
            failing.Data.Qualities = n => throw FakeHomeSource.Fail(503);
            await failing.Session.PrepareAsync(CancellationToken.None);
            Assert.AreEqual(PlayerState.Failed, failing.Session.State);
            Assert.AreEqual(PlayerFailure.Network, failing.Session.Failure);
        }

        [TestMethod]
        public async Task SubtitleAndSkipFailures_NeverBlockPlayback()
        {
            var h = new Harness();
            h.Data.Translations = () => throw FakeHomeSource.Fail();
            h.Data.Skip = () => throw FakeHomeSource.Fail();
            await h.Session.PrepareAsync(CancellationToken.None);
            Assert.AreEqual(PlayerState.Ready, h.Session.State);
            Assert.AreEqual(0, h.Session.Subtitles.Count);
        }

        [TestMethod]
        public async Task OnlySubtitleTracksWithAUsableHttpsAddress_AreOffered()
        {
            var h = new Harness();
            h.Data.Translations = () => Task.FromResult(FakePlaybackData.Subs(ar: "relative/path/ar.srt", en: "https://subs.invalid/en.srt", sp: "http://insecure.invalid/sp.srt"));
            await h.Session.PrepareAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { SubtitleLanguage.English }, h.Session.Subtitles.Select(s => s.Language).ToArray());
        }

        [TestMethod]
        public async Task NoSubtitleFiles_MeansNoSubtitleControls()
        {
            var h = new Harness();
            h.Data.Translations = () => Task.FromResult(FakePlaybackData.Subs(ar: string.Empty, en: string.Empty));
            await h.Session.PrepareAsync(CancellationToken.None);
            Assert.AreEqual(0, h.Session.Subtitles.Count);
            Assert.IsNull(h.Session.CurrentSubtitle);
        }

        [TestMethod]
        public async Task RememberedSubtitle_IsAppliedOnlyWhenThatLanguageExists()
        {
            var settings = new FakeSettingsStore();
            new PlayerPreferences(settings).Subtitle = SubtitlePreference.English;
            var present = new Harness(settings: settings);
            await present.PlayAsync();
            Assert.AreEqual(SubtitleLanguage.English, present.Session.CurrentSubtitle);
            Assert.AreEqual(SubtitleLanguage.English, present.Engine.LastSubtitle);

            var missing = new Harness(settings: settings);
            missing.Data.Translations = () => Task.FromResult(FakePlaybackData.Subs(en: string.Empty));
            await missing.PlayAsync();
            Assert.IsNull(missing.Session.CurrentSubtitle, "never silently switches to another language");

            var off = new FakeSettingsStore();
            new PlayerPreferences(off).Subtitle = SubtitlePreference.Off;
            var disabled = new Harness(settings: off);
            await disabled.PlayAsync();
            Assert.IsNull(disabled.Session.CurrentSubtitle);
        }

        // ---- start, resume ----------------------------------------------------------------------

        [TestMethod]
        public async Task StartOver_OpensAtZero_AndPlaysWhenTheEngineIsReady()
        {
            var h = new Harness();
            await h.Session.PrepareAsync(CancellationToken.None);
            h.Session.Start(PlaybackStartMode.StartOver);
            Assert.AreEqual(PlayerState.Opening, h.Session.State);
            Assert.AreEqual(TimeSpan.Zero, h.Engine.Opens.Single().Start);
            Assert.AreEqual("720p", h.Engine.Opens.Single().Source.QualityLabel);

            h.Engine.RaiseOpened();
            Assert.AreEqual(PlayerState.Playing, h.Session.State);
            Assert.IsTrue(h.Engine.Calls.Contains("play"));
        }

        [TestMethod]
        public async Task Resume_UsesTheSavedPosition_MinusTheRewind()
        {
            var h = new Harness();
            h.Progress.Save(new PlaybackProgress("192319", null, null, null, TimeSpan.FromSeconds(600), TimeSpan.FromSeconds(3000), DateTime.UtcNow, false));
            await h.Session.PrepareAsync(CancellationToken.None);
            Assert.IsTrue(h.Session.CanResume);
            h.Session.Start(PlaybackStartMode.Resume);
            Assert.AreEqual(TimeSpan.FromSeconds(597), h.Engine.Opens.Single().Start);
        }

        [TestMethod]
        public async Task Resume_WithoutMeaningfulProgress_StartsAtTheBeginning_AndOffersNoPrompt()
        {
            var h = new Harness();
            h.Progress.Save(new PlaybackProgress("192319", null, null, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3000), DateTime.UtcNow, false));
            await h.Session.PrepareAsync(CancellationToken.None);
            Assert.IsFalse(h.Session.CanResume);
            h.Session.Start(PlaybackStartMode.Resume);
            Assert.AreEqual(TimeSpan.Zero, h.Engine.Opens.Single().Start);

            var done = new Harness();
            done.Progress.Save(new PlaybackProgress("192319", null, null, null, TimeSpan.FromSeconds(2990), TimeSpan.FromSeconds(3000), DateTime.UtcNow, true));
            await done.Session.PrepareAsync(CancellationToken.None);
            Assert.IsFalse(done.Session.CanResume);
        }

        [TestMethod]
        public async Task Start_OnlyWorksFromReady()
        {
            var h = new Harness();
            h.Session.Start(PlaybackStartMode.StartOver);
            Assert.AreEqual(0, h.Engine.Opens.Count);

            await h.PlayAsync();
            h.Session.Start(PlaybackStartMode.StartOver);
            Assert.AreEqual(1, h.Engine.Opens.Count);
        }

        [TestMethod]
        public async Task PausingBeforeTheStreamOpens_IsHonoured()
        {
            var h = new Harness();
            await h.Session.PrepareAsync(CancellationToken.None);
            h.Session.Start(PlaybackStartMode.StartOver);
            h.Session.Pause();
            h.Engine.RaiseOpened();
            Assert.AreEqual(PlayerState.Paused, h.Session.State);
            Assert.IsFalse(h.Engine.Calls.Contains("play"));
        }

        [TestMethod]
        public async Task Buffering_IsReported()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Engine.RaiseBuffering(true);
            Assert.IsTrue(h.Session.IsBuffering);
            h.Engine.RaiseBuffering(false);
            Assert.IsFalse(h.Session.IsBuffering);
        }

        // ---- transport --------------------------------------------------------------------------

        [TestMethod]
        public async Task TogglePlayPause_PausesAndResumes_AndPausingSavesProgress()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(3000));

            h.Session.TogglePlayPause();
            Assert.AreEqual(PlayerState.Paused, h.Session.State);
            Assert.IsTrue(h.Engine.Calls.Contains("pause"));
            Assert.AreEqual(TimeSpan.FromSeconds(120), h.Progress.Get("192319")!.Position);

            h.Session.TogglePlayPause();
            Assert.AreEqual(PlayerState.Playing, h.Session.State);
        }

        [TestMethod]
        public async Task SeekBy_Accelerates_AndClampsToTheMedia()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(3000));

            h.Session.SeekBy(1);
            Assert.AreEqual(TimeSpan.FromSeconds(110), h.Session.Position);
            h.Session.SeekBy(1);
            h.Session.SeekBy(1);
            Assert.AreEqual(TimeSpan.FromSeconds(140), h.Session.Position, "10 + 10 + 20 seconds: repeated presses accelerate");
            Assert.IsTrue(h.Engine.Calls.Contains("seek:110"));

            h.Session.SeekTo(TimeSpan.FromSeconds(99999));
            Assert.AreEqual(TimeSpan.FromSeconds(2999), h.Session.Position, "never past the last second");
            h.Session.SeekTo(TimeSpan.FromSeconds(-50));
            Assert.AreEqual(TimeSpan.Zero, h.Session.Position);
        }

        [TestMethod]
        public async Task SeekingBeforeTheStreamIsOpen_DoesNothing()
        {
            var h = new Harness();
            await h.Session.PrepareAsync(CancellationToken.None);
            h.Session.SeekBy(1);
            Assert.AreEqual(0, h.Engine.Calls.Count);
        }

        // ---- progress ---------------------------------------------------------------------------

        [TestMethod]
        public async Task Progress_IsWrittenPeriodically_NotOnEveryTick()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Clock.Advance(TimeSpan.FromSeconds(16));
            h.Session.Tick(TimeSpan.FromSeconds(16), TimeSpan.FromSeconds(3000));
            Assert.AreEqual(TimeSpan.FromSeconds(16), h.Progress.Get("192319")!.Position);

            h.Clock.Advance(TimeSpan.FromSeconds(2));
            h.Session.Tick(TimeSpan.FromSeconds(18), TimeSpan.FromSeconds(3000));
            Assert.AreEqual(TimeSpan.FromSeconds(16), h.Progress.Get("192319")!.Position, "too soon since the last write");

            h.Clock.Advance(TimeSpan.FromSeconds(16));
            h.Session.Tick(TimeSpan.FromSeconds(34), TimeSpan.FromSeconds(3000));
            Assert.AreEqual(TimeSpan.FromSeconds(34), h.Progress.Get("192319")!.Position);
        }

        [TestMethod]
        public async Task AFewSecondsOfWatching_IsNotSaved()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3000));
            h.Session.Stop();
            Assert.IsNull(h.Progress.Get("192319"));
        }

        [TestMethod]
        public async Task Stop_SavesProgress_StopsTheEngine_AndGoesIdle()
        {
            var h = new Harness(Episode());
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(700), TimeSpan.FromSeconds(1500));
            h.Session.Stop();

            var saved = h.Progress.Get("1119122")!;
            Assert.AreEqual(TimeSpan.FromSeconds(700), saved.Position);
            Assert.AreEqual("1119119", saved.SeriesId);
            Assert.AreEqual(1, saved.Season);
            Assert.AreEqual(2, saved.Episode);
            Assert.IsFalse(saved.Completed);
            Assert.IsTrue(h.Engine.Calls.Contains("stop"));
            Assert.AreEqual(PlayerState.Idle, h.Session.State);
        }

        [TestMethod]
        public async Task NearTheEnd_ProgressIsMarkedCompleted()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(2950), TimeSpan.FromSeconds(3000));
            h.Session.Flush();
            var saved = h.Progress.Get("192319")!;
            Assert.IsTrue(saved.Completed);
            Assert.AreEqual(TimeSpan.FromSeconds(3000), saved.Position);
        }

        // ---- recovery ---------------------------------------------------------------------------

        [TestMethod]
        public async Task NetworkFailure_RetriesTheSameQuality_WithAFreshAddress_AtTheSamePosition()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(3000));
            var before = h.Engine.Opens.Last().Source.Media;

            h.Engine.RaiseFailed(PlayerFailure.Network);
            Assert.IsTrue(h.Session.IsRecovering);
            await WaitFor(() => h.Engine.Opens.Count == 2, "the retry to open");

            Assert.AreEqual(720, h.Session.CurrentQuality!.Height);
            Assert.AreEqual(TimeSpan.FromSeconds(300), h.Engine.Opens.Last().Start);
            Assert.AreNotEqual(before, h.Engine.Opens.Last().Source.Media, "signed addresses expire: a failure re-requests them");
            Assert.AreEqual(2, h.Data.QualityCalls);
        }

        [TestMethod]
        public async Task RepeatedFailures_FallBackToALowerQuality_KeepingThePosition()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(3000));

            h.Engine.RaiseFailed(PlayerFailure.Network);
            await WaitFor(() => h.Engine.Opens.Count == 2, "retry");
            h.Engine.RaiseOpened();
            h.Engine.RaiseFailed(PlayerFailure.Network);
            await WaitFor(() => h.Engine.Opens.Count == 3, "fallback");

            Assert.AreEqual(480, h.Session.CurrentQuality!.Height);
            Assert.AreEqual(720, h.Session.FellBackFromHeight);
            Assert.AreEqual(TimeSpan.FromSeconds(300), h.Engine.Opens.Last().Start);
        }

        [TestMethod]
        public async Task AnUnplayableStream_SkipsTheRetry_AndFallsBackImmediately()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Engine.Opens.Count == 2, "fallback");
            Assert.AreEqual(480, h.Session.CurrentQuality!.Height);

            h.Engine.RaiseOpened();
            h.Engine.RaiseFailed(PlayerFailure.Decoding);
            await WaitFor(() => h.Engine.Opens.Count == 3, "second fallback");
            Assert.AreEqual(240, h.Session.CurrentQuality!.Height, "next lower quality (this video has no 360p)");
        }

        [TestMethod]
        public async Task WhenEveryQualityFails_ThePlayerFailsInsteadOfLoopingForever_AndRetryRecovers()
        {
            var h = new Harness();
            h.Data.Qualities = n => Task.FromResult(Q.WithGeneration(n, 360, 720));
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(3000));

            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia); // 720 -> 360
            await WaitFor(() => h.Engine.Opens.Count == 2, "fallback to 360");
            h.Engine.RaiseOpened();
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia); // nothing left

            await WaitFor(() => h.Session.State == PlayerState.Failed, "the failed state");
            Assert.AreEqual(PlayerFailure.InvalidMedia, h.Session.Failure);
            Assert.AreEqual(2, h.Engine.Opens.Count);

            h.Data.Qualities = n => Task.FromResult(Q.WithGeneration(n + 100, 360, 720));
            await h.Session.RetryAsync(CancellationToken.None);
            Assert.AreEqual(3, h.Engine.Opens.Count);
            Assert.AreEqual(TimeSpan.FromSeconds(200), h.Engine.Opens.Last().Start, "retry resumes where playback stopped");
            Assert.AreEqual(720, h.Session.CurrentQuality!.Height, "the quality order starts over");
            Assert.AreEqual(PlayerState.Opening, h.Session.State);
        }

        [TestMethod]
        public async Task ASteadyStream_ForgetsOldFailures()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Engine.RaiseFailed(PlayerFailure.Network);
            await WaitFor(() => h.Engine.Opens.Count == 2, "retry");
            h.Engine.RaiseOpened();
            h.Session.Tick(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(3000)); // 30 s of steady playback
            Assert.IsFalse(h.Session.IsRecovering);

            h.Engine.RaiseFailed(PlayerFailure.Network);
            await WaitFor(() => h.Engine.Opens.Count == 3, "another retry on the same quality");
            Assert.AreEqual(720, h.Session.CurrentQuality!.Height, "a failure after steady playback is not the second strike");
        }

        [TestMethod]
        public async Task FailuresWhileNotPlaying_AreIgnored()
        {
            var h = new Harness();
            h.Engine.RaiseFailed(PlayerFailure.Network);
            await h.Session.PrepareAsync(CancellationToken.None);
            h.Engine.RaiseFailed(PlayerFailure.Network);
            Assert.AreEqual(PlayerState.Ready, h.Session.State);
            Assert.AreEqual(0, h.Engine.Opens.Count);
        }

        [TestMethod]
        public async Task SelectQuality_FromTheFailureScreen_RetriesAtThatQuality_InsteadOfRequiringAPlainRetryFirst()
        {
            // PROMPT.md section 59, Player Error: "Retry / Change Quality / Go Back" are three independent ways out of a failure,
            // not Retry-then-Change-Quality; choosing a quality directly from the failure screen must work on its own.
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(3000));
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Engine.Opens.Count == 2, "fallback to 480p");
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Engine.Opens.Count == 3, "fallback to 360p");
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Engine.Opens.Count == 4, "fallback to 240p");
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Session.State == PlayerState.Failed, "every quality exhausted");

            await h.Session.SelectQualityAsync(h.Session.Qualities.First(q => q.Height == 1080), CancellationToken.None);
            Assert.AreEqual(PlayerState.Opening, h.Session.State);
            Assert.AreEqual(PlayerFailure.None, h.Session.Failure);
            Assert.AreEqual(1080, h.Session.CurrentQuality!.Height);
            Assert.AreEqual(TimeSpan.FromSeconds(300), h.Engine.Opens.Last().Start, "resumes where playback stopped, not from zero");
        }

        [TestMethod]
        public async Task SelectQuality_RemembersIt_AndReopensAtThePosition_KeepingPauseState()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(500), TimeSpan.FromSeconds(3000));
            h.Session.Pause();

            await h.Session.SelectQualityAsync(h.Session.Qualities.First(q => q.Height == 1080), CancellationToken.None);
            Assert.AreEqual(1080, h.Session.CurrentQuality!.Height);
            Assert.AreEqual(1080, h.Preferences.PreferredQualityHeight);
            Assert.AreEqual(TimeSpan.FromSeconds(500), h.Engine.Opens.Last().Start);

            var playsBefore = h.Engine.Calls.Count(c => c == "play");
            h.Engine.RaiseOpened();
            Assert.AreEqual(PlayerState.Paused, h.Session.State, "a paused viewer stays paused after changing quality");
            Assert.AreEqual(playsBefore, h.Engine.Calls.Count(c => c == "play"));
        }

        // ---- subtitles --------------------------------------------------------------------------

        [TestMethod]
        public async Task SelectSubtitle_SwitchesTheTrack_RemembersIt_AndIgnoresUnavailableOnes()
        {
            var h = new Harness();
            await h.PlayAsync();

            h.Session.SelectSubtitle(SubtitleLanguage.Arabic);
            Assert.AreEqual(SubtitleLanguage.Arabic, h.Session.CurrentSubtitle);
            Assert.AreEqual(SubtitlePreference.Arabic, h.Preferences.Subtitle);

            h.Session.SelectSubtitle(SubtitleLanguage.Spanish);
            Assert.AreEqual(SubtitleLanguage.Arabic, h.Session.CurrentSubtitle, "Spanish has no file in this fixture");

            h.Session.SelectSubtitle(null);
            Assert.IsNull(h.Session.CurrentSubtitle);
            Assert.AreEqual(SubtitlePreference.Off, h.Preferences.Subtitle);
            Assert.IsTrue(h.Engine.Calls.Contains("subtitle:off"));
        }

        // ---- ending, next episode ---------------------------------------------------------------

        [TestMethod]
        public async Task Ending_MarksTheEpisodeCompleted_AndAdvancesWhenAutoplayIsOn()
        {
            var h = new Harness(Episode());
            var advanced = 0;
            h.Session.AdvanceRequested += (s, e) => advanced++;
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(1499), TimeSpan.FromSeconds(1500));

            h.Engine.RaiseEnded();
            Assert.AreEqual(PlayerState.Ended, h.Session.State);
            Assert.IsTrue(h.Progress.Get("1119122")!.Completed);
            Assert.AreEqual(1, advanced);

            h.Session.Tick(TimeSpan.FromSeconds(1500), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(1, advanced, "the next episode is requested once");
        }

        [TestMethod]
        public async Task AutoplayOff_NoNextEpisode_OrACancelledOffer_NeverAdvance()
        {
            var off = new Harness(Episode());
            off.Preferences.AutoplayNext = false;
            var offAdvanced = 0;
            off.Session.AdvanceRequested += (s, e) => offAdvanced++;
            await off.PlayAsync();
            off.Session.Tick(TimeSpan.FromSeconds(1495), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(AutoNextPhase.Offering, off.Session.AutoNextPhase, "the offer is still shown, but only manually");
            off.Engine.RaiseEnded();
            Assert.AreEqual(0, offAdvanced);

            var last = new Harness(Episode(withNext: false));
            var lastAdvanced = 0;
            last.Session.AdvanceRequested += (s, e) => lastAdvanced++;
            await last.PlayAsync();
            last.Session.Tick(TimeSpan.FromSeconds(1495), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(AutoNextPhase.Hidden, last.Session.AutoNextPhase);
            last.Engine.RaiseEnded();
            Assert.AreEqual(0, lastAdvanced);

            var cancelled = new Harness(Episode());
            var cancelledAdvanced = 0;
            cancelled.Session.AdvanceRequested += (s, e) => cancelledAdvanced++;
            await cancelled.PlayAsync();
            cancelled.Session.Tick(TimeSpan.FromSeconds(1490), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(AutoNextPhase.Offering, cancelled.Session.AutoNextPhase);
            cancelled.Session.CancelAutoNext();
            Assert.AreEqual(AutoNextPhase.Hidden, cancelled.Session.AutoNextPhase);
            cancelled.Engine.RaiseEnded();
            Assert.AreEqual(0, cancelledAdvanced);
        }

        [TestMethod]
        public async Task TheOffer_AppearsInTheFinalSeconds_WithACountdown()
        {
            var h = new Harness(Episode());
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(1000), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(AutoNextPhase.Hidden, h.Session.AutoNextPhase);
            h.Session.Tick(TimeSpan.FromSeconds(1492), TimeSpan.FromSeconds(1500));
            Assert.AreEqual(AutoNextPhase.Offering, h.Session.AutoNextPhase);
            Assert.IsTrue(h.Session.AutoNextShowsCountdown);
            Assert.AreEqual(8, h.Session.AutoNextSecondsRemaining);
        }

        [TestMethod]
        public async Task ReplayAfterTheEnd_RestartsPlayback()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(2999), TimeSpan.FromSeconds(3000));
            h.Engine.RaiseEnded();
            h.Session.TogglePlayPause();
            Assert.AreEqual(PlayerState.Playing, h.Session.State);
            Assert.IsTrue(h.Engine.Calls.Contains("seek:0"));
        }

        // ---- skip -------------------------------------------------------------------------------

        [TestMethod]
        public async Task SkipIntro_IsHiddenByDefault_BecauseTheUnitIsUnverified()
        {
            var h = new Harness(Episode(intro: true));
            h.Data.Skip = () => Task.FromResult(PlaybackMapper.ToSkipInfo(JsonParser.Parse("{\"start\":[10],\"end\":[90]}")));
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1500));
            Assert.IsNull(h.Session.ActiveSkip);
            h.Session.SkipIntro();
            Assert.IsFalse(h.Engine.Calls.Any(c => c.StartsWith("seek:")));
        }

        [TestMethod]
        public async Task SkipIntro_WorksOnceAUnitIsConfigured_AndSeeksToTheEndOfTheRange()
        {
            var h = new Harness(Episode(intro: true), SkipTimeUnit.Seconds);
            h.Data.Skip = () => Task.FromResult(PlaybackMapper.ToSkipInfo(JsonParser.Parse("{\"start\":[10],\"end\":[90]}")));
            await h.PlayAsync();

            h.Session.Tick(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1500));
            Assert.IsNull(h.Session.ActiveSkip);
            h.Session.Tick(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1500));
            Assert.IsNotNull(h.Session.ActiveSkip);

            h.Session.SkipIntro();
            Assert.IsTrue(h.Engine.Calls.Contains("seek:90"));
        }

        [TestMethod]
        public async Task TheServerFlag_CanHideSkipIntro_EvenWithTimingData()
        {
            var h = new Harness(Episode(intro: false), SkipTimeUnit.Seconds);
            h.Data.Skip = () => Task.FromResult(PlaybackMapper.ToSkipInfo(JsonParser.Parse("{\"start\":[10],\"end\":[90]}")));
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(1500));
            Assert.IsNull(h.Session.ActiveSkip);
        }

        // ---- privacy ----------------------------------------------------------------------------

        [TestMethod]
        public async Task Logs_NeverContainStreamOrSubtitleAddresses()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Tick(TimeSpan.FromSeconds(300), TimeSpan.FromSeconds(3000));
            h.Engine.RaiseFailed(PlayerFailure.Network);
            await WaitFor(() => h.Engine.Opens.Count == 2, "retry");
            h.Engine.RaiseOpened();
            h.Engine.RaiseFailed(PlayerFailure.InvalidMedia);
            await WaitFor(() => h.Engine.Opens.Count == 3, "fallback");

            Assert.IsTrue(h.Logger.Messages.Count > 0);
            foreach (var message in h.Logger.Messages)
            {
                Assert.IsFalse(message.Contains("SECRET"), message);
                Assert.IsFalse(message.Contains("cdn.invalid"), message);
                Assert.IsFalse(message.Contains("subs.invalid"), message);
                Assert.IsFalse(message.Contains("://"), message);
            }
        }

        [TestMethod]
        public async Task SourceObjects_DoNotRevealTheirAddresses_WhenPrinted()
        {
            var h = new Harness();
            await h.PlayAsync();
            var source = h.Engine.Opens.Single().Source;
            Assert.AreEqual("720p", source.ToString());
            Assert.IsFalse(source.Subtitles.Any(s => s.ToString().Contains("http")));
        }

        [TestMethod]
        public async Task ADisposedSession_IgnoresEngineEvents()
        {
            var h = new Harness();
            await h.PlayAsync();
            h.Session.Dispose();
            h.Engine.RaiseFailed(PlayerFailure.Network);
            h.Engine.RaiseEnded();
            Assert.AreEqual(1, h.Engine.Opens.Count);
        }
    }
}
