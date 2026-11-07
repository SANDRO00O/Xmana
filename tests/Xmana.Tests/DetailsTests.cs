using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Caching;
using Xmana.Core.Details;
using Xmana.Core.Discovery;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;
using Xmana.Core.Navigation;

namespace Xmana.Tests
{
    internal sealed class FakeDetailsSource : IDetailsDataSource
    {
        public List<string> Calls { get; } = new List<string>();

        public Func<string, Task<ApiResult<VideoDetails?>>> Info { get; set; } = id => Task.FromResult(new ApiResult<VideoDetails?>(Detail("1", id), DataOrigin.Network, null));

        public Func<string, Task<ApiResult<IReadOnlyList<SeasonInfo>>>> Seasons { get; set; } = id => Task.FromResult(new ApiResult<IReadOnlyList<SeasonInfo>>(new SeasonInfo[0], DataOrigin.Network, null));

        public Func<string, Task<ApiResult<IReadOnlyList<VideoSummary>>>> Episodes { get; set; } = id => FakeDiscoverySource.Ok();

        public Func<string, string, Task<ApiResult<IReadOnlyList<VideoSummary>>>> Related { get; set; } = (id, kind) => FakeDiscoverySource.Ok();

        public Func<string, string, Task<RecommendationResult>> Recommendations { get; set; } = (id, title) => Task.FromResult(ExternalServiceMapper.ToRecommendations(null));

        public Func<string, string, Task<ApiResult<StaffPage>>> Staff { get; set; } =
            (id, lang) => Task.FromResult(new ApiResult<StaffPage>(new StaffPage(null, new VideoSummary[0], new JObject()), DataOrigin.Network, null));

        public string? LastRecommendationTitle { get; private set; }

        public string? LastStaffLanguage { get; private set; }

        /// <summary>A video object: kind and rootSeries decide movie / series / episode.</summary>
        public static VideoDetails Detail(string kind, string id, string rootSeries = "0", string title = "T", string season = "", string episode = "")
        {
            var json = "{\"nb\":\"" + id + "\",\"en_title\":\"" + title + "\",\"kind\":\"" + kind + "\",\"rootSeries\":\"" + rootSeries + "\""
                + (season.Length > 0 ? ",\"season\":\"" + season + "\"" : string.Empty)
                + (episode.Length > 0 ? ",\"episodeNummer\":\"" + episode + "\"" : string.Empty) + "}";
            return VideoMapper.ToDetails(JsonParser.Parse(json))!;
        }

        public static IReadOnlyList<VideoSummary> Eps(params string[] specs) =>
            VideoMapper.ToSummaries(specs.Select(s =>
            {
                var p = s.Split('/'); // id/season/episode (season or episode may be empty)
                return JsonParser.Parse("{\"nb\":\"" + p[0] + "\"" + (p[1].Length > 0 ? ",\"season\":\"" + p[1] + "\"" : string.Empty) + (p[2].Length > 0 ? ",\"episodeNummer\":\"" + p[2] + "\"" : string.Empty) + "}");
            }));

        public static Task<ApiResult<IReadOnlyList<VideoSummary>>> EpisodesOf(params string[] specs) =>
            Task.FromResult(new ApiResult<IReadOnlyList<VideoSummary>>(Eps(specs), DataOrigin.Network, null));

        public static Task<ApiResult<IReadOnlyList<SeasonInfo>>> SeasonsOf(params int[] numbers) =>
            Task.FromResult(new ApiResult<IReadOnlyList<SeasonInfo>>(numbers.Select(n => new SeasonInfo(n, new JObject())).ToList(), DataOrigin.Network, null));

        public static RecommendationResult RecsOf(params string[] ids) =>
            ExternalServiceMapper.ToRecommendations(new JArray(ids.Select(i => JObject.Parse("{\"nb\":\"" + i + "\",\"en_title\":\"R" + i + "\"}"))));

        private void Record(string call)
        {
            lock (Calls)
            {
                Calls.Add(call);
            }
        }

        public Task<ApiResult<VideoDetails?>> GetVideoInfoAsync(string id, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("info:" + id);
            return Info(id);
        }

        public Task<ApiResult<IReadOnlyList<SeasonInfo>>> GetSeasonsAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("seasons:" + seriesId);
            return Seasons(seriesId);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetEpisodesAsync(string seriesId, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("episodes:" + seriesId);
            return Episodes(seriesId);
        }

        public Task<ApiResult<IReadOnlyList<VideoSummary>>> GetRelatedAsync(string id, string kind, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("related:" + id + ":" + kind);
            return Related(id, kind);
        }

        public Task<RecommendationResult> GetRecommendationsAsync(string videoId, string title, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("recommend:" + videoId);
            LastRecommendationTitle = title;
            return Recommendations(videoId, title);
        }

        public Task<ApiResult<StaffPage>> GetStaffAsync(string staffId, string lang, RequestPriority priority, CancellationToken cancellationToken)
        {
            Record("staff:" + staffId);
            LastStaffLanguage = lang;
            return Staff(staffId, lang);
        }
    }

    [TestClass]
    public class SeriesContextTests
    {
        private static IReadOnlyList<VideoSummary> Sample() =>
            FakeDetailsSource.Eps("e21/2/1", "e12/1/2", "e11/1/1", "e22/2/2", "e13/1/3");

        [TestMethod]
        public void Groups_AreOrderedBySeason_AndEpisodesByNumber_RegardlessOfServerOrder()
        {
            var context = SeriesContext.Build("S", Sample(), new int[0], null);
            CollectionAssert.AreEqual(new int?[] { 1, 2 }, context.Groups.Select(g => g.Season).ToArray());
            CollectionAssert.AreEqual(new[] { "e11", "e12", "e13" }, context.Groups[0].Episodes.Select(e => e.Id).ToArray());
            Assert.AreEqual(5, context.TotalEpisodes);
            Assert.IsTrue(context.HasEpisodes);
        }

        [TestMethod]
        public void WithoutACurrentEpisode_TheLowestSeasonIsSelected_AndNothingIsPreviousOrNext()
        {
            var context = SeriesContext.Build("S", Sample(), new int[0], null);
            Assert.AreEqual(1, context.SelectedSeason);
            Assert.IsTrue(context.HasSelection);
            CollectionAssert.AreEqual(new[] { "e11", "e12", "e13" }, context.SelectedEpisodes.Select(e => e.Id).ToArray());
            Assert.IsNull(context.CurrentEpisodeId);
            Assert.IsNull(context.Previous);
            Assert.IsNull(context.Next);
        }

        [TestMethod]
        public void ACurrentEpisode_SelectsItsSeason_AndPreviousNextCrossSeasonBoundaries()
        {
            var last = SeriesContext.Build("S", Sample(), new int[0], "e13");
            Assert.AreEqual(1, last.SelectedSeason);
            Assert.AreEqual("e12", last.Previous!.Id);
            Assert.AreEqual("e21", last.Next!.Id, "the next episode after the last of season 1 is the first of season 2");

            var first = SeriesContext.Build("S", Sample(), new int[0], "e21");
            Assert.AreEqual(2, first.SelectedSeason);
            Assert.AreEqual("e13", first.Previous!.Id);

            var end = SeriesContext.Build("S", Sample(), new int[0], "e22");
            Assert.IsNull(end.Next);
            Assert.AreEqual("e22", end.CurrentEpisodeId);
        }

        [TestMethod]
        public void AnUnknownCurrentEpisode_IsIgnored()
        {
            var context = SeriesContext.Build("S", Sample(), new int[0], "nope");
            Assert.IsNull(context.CurrentEpisodeId);
            Assert.IsNull(context.Previous);
            Assert.AreEqual(1, context.SelectedSeason);
        }

        [TestMethod]
        public void SeasonNumbersFromTheServer_AddEmptySeasons()
        {
            var context = SeriesContext.Build("S", FakeDetailsSource.Eps("e11/1/1"), new[] { 1, 2, 3 }, null);
            CollectionAssert.AreEqual(new int?[] { 1, 2, 3 }, context.Groups.Select(g => g.Season).ToArray());
            Assert.AreEqual(0, context.Groups[2].Episodes.Count);

            var selected = context.WithSeason(3, SeriesContext.FlatOrder(context.Groups));
            Assert.AreEqual(3, selected.SelectedSeason);
            Assert.AreEqual(0, selected.SelectedEpisodes.Count);
        }

        [TestMethod]
        public void EmptySeasonsAreNotPreferredAsTheDefault()
        {
            var context = SeriesContext.Build("S", FakeDetailsSource.Eps("e21/2/1"), new[] { 1, 2 }, null);
            Assert.AreEqual(2, context.SelectedSeason, "season 1 has no episodes, so the first season with episodes is selected");
        }

        [TestMethod]
        public void EpisodesWithoutASeason_FormALastGroup()
        {
            var context = SeriesContext.Build("S", FakeDetailsSource.Eps("a/1/1", "x//1"), new int[0], null);
            CollectionAssert.AreEqual(new int?[] { 1, null }, context.Groups.Select(g => g.Season).ToArray());

            var current = SeriesContext.Build("S", FakeDetailsSource.Eps("a/1/1", "x//1"), new int[0], "x");
            Assert.IsNull(current.SelectedSeason);
            Assert.IsTrue(current.HasSelection);
            Assert.AreEqual("x", current.SelectedEpisodes.Single().Id);
        }

        [TestMethod]
        public void NoDataAtAll_MeansNoSelectionAndNoEpisodes()
        {
            var context = SeriesContext.Build("S", new VideoSummary[0], new int[0], null);
            Assert.IsFalse(context.HasEpisodes);
            Assert.IsFalse(context.HasSelection);
            Assert.AreEqual(0, context.Groups.Count);
            Assert.AreEqual(0, context.SelectedEpisodes.Count);
        }
    }

    [TestClass]
    public class DetailsControllerTests
    {
        private static DetailsController Create(FakeDetailsSource source, string id = "10", string lang = "en") =>
            new DetailsController(source, id, () => lang);

        [TestMethod]
        public async Task Movie_LoadsMetadataRelatedAndRecommendations_WithoutAnySeriesRequests()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "Conan Movie"), DataOrigin.Network, null)),
                Related = (id, kind) => FakeDiscoverySource.Ok("r1", "r2"),
                Recommendations = (id, title) => Task.FromResult(FakeDetailsSource.RecsOf("c1")),
            };
            var details = Create(source, "192319");
            await details.LoadAsync(CancellationToken.None);

            Assert.AreEqual(DetailsState.Content, details.State);
            Assert.AreEqual("Conan Movie", details.Details!.Summary.EnTitle);
            Assert.IsFalse(details.IsEpisode);
            Assert.AreEqual(SeriesLoadState.None, details.SeriesState);
            CollectionAssert.AreEqual(new[] { "r1", "r2" }, details.Related.Select(v => v.Id).ToArray());
            Assert.AreEqual(0, source.Calls.Count(c => c.StartsWith("seasons") || c.StartsWith("episodes")));
            Assert.AreEqual("Conan Movie", source.LastRecommendationTitle);
        }

        [TestMethod]
        public async Task Series_LoadsSeasonsAndEpisodes_ForItsOwnId()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id), DataOrigin.Network, null)),
                Seasons = id => FakeDetailsSource.SeasonsOf(1, 2),
                Episodes = id => FakeDetailsSource.EpisodesOf("e21/2/1", "e11/1/1", "e12/1/2"),
            };
            var details = Create(source, "1119119");
            await details.LoadAsync(CancellationToken.None);

            Assert.IsTrue(source.Calls.Contains("episodes:1119119"));
            Assert.IsTrue(source.Calls.Contains("seasons:1119119"));
            Assert.AreEqual(SeriesLoadState.Loaded, details.SeriesState);
            Assert.AreEqual(1, details.Series!.SelectedSeason);
            Assert.IsNull(details.Series.CurrentEpisodeId, "the series itself is not an episode");
            Assert.IsFalse(details.IsEpisode);
        }

        [TestMethod]
        public async Task Episode_UsesTheRootSeriesForEpisodes_AndKnowsItsPlace()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(
                    id == "1119122" ? FakeDetailsSource.Detail("2", id, "1119119", "Episode Two", "1", "2") : FakeDetailsSource.Detail("2", id, title: "Detective Conan"),
                    DataOrigin.Network,
                    null)),
                Seasons = id => FakeDetailsSource.SeasonsOf(1, 2),
                Episodes = id => FakeDetailsSource.EpisodesOf("1119121/1/1", "1119122/1/2", "1119123/1/3", "1119200/2/1"),
            };
            var details = Create(source, "1119122");
            await details.LoadAsync(CancellationToken.None);

            Assert.IsTrue(details.IsEpisode);
            Assert.IsTrue(source.Calls.Contains("episodes:1119119"), "episodes come from the root series, never from the episode id");
            Assert.IsFalse(source.Calls.Contains("episodes:1119122"));
            Assert.AreEqual("1119122", details.Series!.CurrentEpisodeId);
            Assert.AreEqual("1119121", details.Series.Previous!.Id);
            Assert.AreEqual("1119123", details.Series.Next!.Id);
            Assert.AreEqual("Detective Conan", details.GetSeriesTitle(false));
        }

        [TestMethod]
        public async Task SeriesTitle_IsOptional_AndFallsBackBetweenLanguages()
        {
            var source = new FakeDetailsSource
            {
                Info = id => id == "1119122"
                    ? Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id, "1119119"), DataOrigin.Network, null))
                    : throw FakeHomeSource.Fail(),
            };
            var details = Create(source, "1119122");
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(DetailsState.Content, details.State, "a failing series-title request must not affect the screen");
            Assert.IsNull(details.GetSeriesTitle(true));
        }

        [TestMethod]
        public async Task MetadataFailure_IsAnError_AndRetryRecovers()
        {
            var fail = true;
            var source = new FakeDetailsSource
            {
                Info = id => fail ? throw FakeHomeSource.Fail() : Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id), DataOrigin.Network, null)),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(DetailsState.Error, details.State);
            Assert.AreEqual(0, source.Calls.Count(c => c.StartsWith("related")), "nothing else is requested without metadata");

            fail = false;
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(DetailsState.Content, details.State);
        }

        [TestMethod]
        public async Task UnknownVideo_IsNotFound_NotAnError()
        {
            var source = new FakeDetailsSource { Info = id => Task.FromResult(new ApiResult<VideoDetails?>(null, DataOrigin.Network, null)) };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(DetailsState.NotFound, details.State);
            Assert.AreEqual(0, source.Calls.Count - 1);
        }

        [TestMethod]
        public async Task AFailedRefresh_KeepsTheContentThatIsAlreadyShown()
        {
            var fail = false;
            var source = new FakeDetailsSource
            {
                Info = id => fail ? throw FakeHomeSource.Fail() : Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "Shown"), DataOrigin.Network, null)),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            fail = true;
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(DetailsState.Content, details.State);
            Assert.AreEqual("Shown", details.Details!.Summary.EnTitle);
        }

        [TestMethod]
        public async Task SeriesFailure_IsIsolated_AndCanBeRetriedOnItsOwn()
        {
            var fail = true;
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id), DataOrigin.Network, null)),
                Seasons = id => fail ? throw FakeHomeSource.Fail() : FakeDetailsSource.SeasonsOf(1),
                Episodes = id => fail ? throw FakeHomeSource.Fail() : FakeDetailsSource.EpisodesOf("e1/1/1"),
                Related = (id, kind) => FakeDiscoverySource.Ok("r1"),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);

            Assert.AreEqual(DetailsState.Content, details.State);
            Assert.AreEqual(SeriesLoadState.Failed, details.SeriesState);
            Assert.AreEqual(1, details.Related.Count, "other sections are unaffected");

            fail = false;
            await details.RetrySeriesAsync(CancellationToken.None);
            Assert.AreEqual(SeriesLoadState.Loaded, details.SeriesState);
            Assert.IsTrue(details.Series!.HasEpisodes);
        }

        [TestMethod]
        public async Task EpisodesFailingButSeasonsWorking_StillShowsTheSeasons()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id), DataOrigin.Network, null)),
                Seasons = id => FakeDetailsSource.SeasonsOf(1, 2),
                Episodes = id => throw FakeHomeSource.Fail(),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual(SeriesLoadState.Loaded, details.SeriesState);
            Assert.AreEqual(2, details.Series!.Groups.Count);
            Assert.IsFalse(details.Series.HasEpisodes);
        }

        [TestMethod]
        public async Task SelectSeason_ChangesTheEpisodes_AndIgnoresUnknownSeasons()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id), DataOrigin.Network, null)),
                Episodes = id => FakeDetailsSource.EpisodesOf("a/1/1", "b/2/1", "c/2/2"),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            var raised = 0;
            details.Changed += (s, e) => raised++;

            details.SelectSeason(2);
            CollectionAssert.AreEqual(new[] { "b", "c" }, details.Series!.SelectedEpisodes.Select(e => e.Id).ToArray());
            Assert.AreEqual(1, raised);

            details.SelectSeason(9);
            Assert.AreEqual(2, details.Series.SelectedSeason);
            Assert.AreEqual(1, raised);
        }

        [TestMethod]
        public async Task Related_RetriesWithTheVerifiedKind_WhenTheVideosOwnKindReturnsNothing()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id), DataOrigin.Network, null)),
                Related = (id, kind) => kind == "1" ? FakeDiscoverySource.Ok("r1") : FakeDiscoverySource.Ok(),
            };
            var details = Create(source, "10");
            await details.LoadAsync(CancellationToken.None);

            CollectionAssert.AreEqual(new[] { "related:10:2", "related:10:1" }, source.Calls.Where(c => c.StartsWith("related")).ToArray());
            Assert.AreEqual(1, details.Related.Count);
        }

        [TestMethod]
        public async Task Related_WithTheVerifiedKindAlready_IsNotRequestedTwice_AndExcludesTheCurrentVideo()
        {
            var source = new FakeDetailsSource
            {
                Related = (id, kind) => FakeDiscoverySource.Ok("10", "r1"),
            };
            var details = Create(source, "10");
            await details.LoadAsync(CancellationToken.None);

            Assert.AreEqual(1, source.Calls.Count(c => c.StartsWith("related")));
            CollectionAssert.AreEqual(new[] { "r1" }, details.Related.Select(v => v.Id).ToArray());
        }

        [TestMethod]
        public async Task RecommendationFailure_NeverBreaksTheScreen_AndRelatedRemains()
        {
            var source = new FakeDetailsSource
            {
                Related = (id, kind) => FakeDiscoverySource.Ok("r1"),
                Recommendations = (id, title) => throw FakeHomeSource.Fail(503),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);

            Assert.AreEqual(DetailsState.Content, details.State);
            Assert.AreEqual(0, details.Recommended.Count);
            Assert.AreEqual(1, details.Related.Count);
        }

        [TestMethod]
        public async Task Recommendations_OnlyKeepItemsThatAreVideoObjects_AndDropTheCurrentVideo()
        {
            var source = new FakeDetailsSource
            {
                Recommendations = (id, title) => Task.FromResult(FakeDetailsSource.RecsOf("10", "c1", "c2")),
            };
            var details = Create(source, "10");
            await details.LoadAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "c1", "c2" }, details.Recommended.Select(v => v.Id).ToArray());

            var onlyIds = new FakeDetailsSource
            {
                Recommendations = (id, title) => Task.FromResult(ExternalServiceMapper.ToRecommendations(JsonParser.Parse("[{\"MovieId\":\"5\"}]"))),
            };
            var bare = Create(onlyIds);
            await bare.LoadAsync(CancellationToken.None);
            Assert.AreEqual(0, bare.Recommended.Count, "an item that is not a video object cannot be shown");
        }

        [TestMethod]
        public async Task RecommendationTitle_FallsBackToTheArabicTitle()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(
                    VideoMapper.ToDetails(JsonParser.Parse("{\"nb\":\"" + id + "\",\"ar_title\":\"عنوان\",\"kind\":\"1\"}")), DataOrigin.Network, null)),
            };
            await Create(source).LoadAsync(CancellationToken.None);
            Assert.AreEqual("عنوان", source.LastRecommendationTitle);
        }

        [TestMethod]
        public async Task OutdatedMetadata_IsFlagged()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id), DataOrigin.CacheAfterError, null)),
            };
            var details = Create(source);
            await details.LoadAsync(CancellationToken.None);
            Assert.IsTrue(details.IsShowingOutdatedData);
        }

        [TestMethod]
        public async Task ANewerLoad_SupersedesAnOlderOne()
        {
            var gate = new TaskCompletionSource<bool>();
            var call = 0;
            var source = new FakeDetailsSource
            {
                Info = async id =>
                {
                    var n = Interlocked.Increment(ref call);
                    if (n == 1)
                    {
                        await gate.Task;
                        return new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "old"), DataOrigin.Network, null);
                    }

                    return new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "new"), DataOrigin.Network, null);
                },
            };
            var details = Create(source);
            var first = details.LoadAsync(CancellationToken.None);
            await details.LoadAsync(CancellationToken.None);
            gate.SetResult(true);
            await first;
            Assert.AreEqual("new", details.Details!.Summary.EnTitle);
        }

        [TestMethod]
        public async Task SwitchToAsync_UpdatesTheSameController_WithoutLosingUnrelatedSections()
        {
            var episodes = new Dictionary<string, string> { { "e1", "Episode One" }, { "e2", "Episode Two" } };
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("2", id, "S", episodes[id], "1", id == "e1" ? "1" : "2"), DataOrigin.Network, null)),
                Episodes = id => FakeDetailsSource.EpisodesOf("e1/1/1", "e2/1/2"),
                Related = (id, kind) => FakeDiscoverySource.Ok("r-" + id),
            };
            var details = Create(source, "e1");
            await details.LoadAsync(CancellationToken.None);
            Assert.AreEqual("Episode One", details.Details!.Summary.EnTitle);
            CollectionAssert.AreEqual(new[] { "r-e1" }, details.Related.Select(v => v.Id).ToArray());

            var raised = 0;
            details.Changed += (s, e) => raised++;
            await details.SwitchToAsync("e2", CancellationToken.None);

            Assert.AreEqual("e2", details.VideoId);
            Assert.AreEqual("Episode Two", details.Details!.Summary.EnTitle);
            Assert.AreEqual("e2", details.Series!.CurrentEpisodeId);
            CollectionAssert.AreEqual(new[] { "r-e2" }, details.Related.Select(v => v.Id).ToArray());
            Assert.IsTrue(raised > 0);
        }

        [TestMethod]
        public async Task SwitchToAsync_ToTheSameId_IsANoOp()
        {
            var source = new FakeDetailsSource();
            var details = Create(source, "10");
            await details.LoadAsync(CancellationToken.None);
            var calls = source.Calls.Count;
            await details.SwitchToAsync("10", CancellationToken.None);
            Assert.AreEqual(calls, source.Calls.Count);
        }

        [TestMethod]
        public async Task SwitchToAsync_DoesNotClearContentWhileTheNewVideoLoads()
        {
            var gate = new TaskCompletionSource<bool>();
            var first = true;
            var source = new FakeDetailsSource
            {
                Info = async id =>
                {
                    if (!first)
                    {
                        await gate.Task;
                    }

                    first = false;
                    return new ApiResult<VideoDetails?>(FakeDetailsSource.Detail("1", id, title: "T" + id), DataOrigin.Network, null);
                },
            };
            var details = Create(source, "a");
            await details.LoadAsync(CancellationToken.None);

            var switching = details.SwitchToAsync("b", CancellationToken.None);
            Assert.AreEqual(DetailsState.Content, details.State, "previous content must stay visible, never a skeleton flash, while switching");
            Assert.AreEqual("Ta", details.Details!.Summary.EnTitle);

            gate.SetResult(true);
            await switching;
            Assert.AreEqual("Tb", details.Details!.Summary.EnTitle);
        }

        [TestMethod]
        public void BlankIdsAreRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => new DetailsController(new FakeDetailsSource(), " "));
            Assert.ThrowsException<ArgumentException>(() => new StaffController(new FakeDetailsSource(), ""));
        }
    }

    [TestClass]
    public class StaffControllerTests
    {
        [TestMethod]
        public async Task Content_ExposesThePersonAndTheirTitles_AndUsesTheLanguage()
        {
            var staff = VideoMapper.ToStaff(JsonParser.Parse("{\"nb\":\"2766\",\"name\":\"Minami Takayama\",\"role\":\"Actor\"}"));
            var source = new FakeDetailsSource
            {
                Staff = (id, lang) => Task.FromResult(new ApiResult<StaffPage>(new StaffPage(staff, FakeHomeSource.Videos("1", "2"), new JObject()), DataOrigin.Network, null)),
            };
            var controller = new StaffController(source, " 2766 ", () => "ar");
            Assert.AreEqual(ResultsState.Idle, controller.State);
            await controller.LoadAsync(CancellationToken.None);

            Assert.AreEqual(ResultsState.Content, controller.State);
            Assert.AreEqual("Minami Takayama", controller.Staff!.Name);
            Assert.AreEqual(2, controller.Titles.Count);
            Assert.AreEqual("ar", source.LastStaffLanguage);
            Assert.IsTrue(source.Calls.Contains("staff:2766"));
            Assert.AreEqual(2, controller.Snapshot.Items.Count);
            Assert.IsFalse(controller.Snapshot.CanLoadMore);
        }

        [TestMethod]
        public async Task PersonWithoutTitles_IsStillContent_ButTheGridSnapshotIsEmpty()
        {
            var staff = VideoMapper.ToStaff(JsonParser.Parse("{\"nb\":\"1\",\"name\":\"X\"}"));
            var source = new FakeDetailsSource
            {
                Staff = (id, lang) => Task.FromResult(new ApiResult<StaffPage>(new StaffPage(staff, new VideoSummary[0], new JObject()), DataOrigin.Network, null)),
            };
            var controller = new StaffController(source, "1");
            await controller.LoadAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Content, controller.State);
            Assert.AreEqual(ResultsState.Empty, controller.Snapshot.State);
        }

        [TestMethod]
        public async Task NothingAtAll_IsEmpty_AndFailureIsAnError()
        {
            var empty = new StaffController(new FakeDetailsSource(), "1");
            await empty.LoadAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Empty, empty.State);

            var failing = new StaffController(new FakeDetailsSource { Staff = (id, lang) => throw FakeHomeSource.Fail() }, "1");
            await failing.LoadAsync(CancellationToken.None);
            Assert.AreEqual(ResultsState.Error, failing.State);
        }
    }

    [TestClass]
    public class NavigationReplaceTests
    {
        [TestMethod]
        public void Replace_SwapsTheTopEntry_WithoutGrowingTheStack()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Details, "e1");
            var depth = nav.Depth;
            var events = new List<NavigationChangedEventArgs>();
            nav.Navigated += (s, e) => events.Add(e);

            var replaced = nav.Replace(NavigationDestination.Details, "e2");

            Assert.AreEqual(depth, nav.Depth);
            Assert.AreSame(replaced, nav.Current);
            Assert.AreEqual("e2", nav.Current.Parameter);
            Assert.AreEqual(NavigationKind.Replace, events.Single().Kind);
            Assert.AreEqual("e1", events.Single().From!.Parameter);
        }

        [TestMethod]
        public void BackAfterReplace_GoesToTheScreenBeforeTheReplacedOne()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Details, "series");
            nav.Replace(NavigationDestination.Details, "ep1");
            nav.Replace(NavigationDestination.Details, "ep2");

            Assert.AreEqual(BackOutcome.NavigatedBack, nav.GoBack());
            Assert.AreEqual(NavigationDestination.Series, nav.Current.Destination);
            Assert.AreEqual(1, nav.Depth);
        }

        [TestMethod]
        public void ReplaceOnARootEntry_BehavesLikePush()
        {
            var nav = new NavigationController();
            nav.Replace(NavigationDestination.Details, "x");
            Assert.AreEqual(2, nav.Depth);
            Assert.AreEqual(NavigationDestination.Home, nav.RootDestination);
        }

        [TestMethod]
        public void RootDestination_IsTheSectionBeneathPushedScreens()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Anime);
            nav.Push(NavigationDestination.Details, "1");
            nav.Push(NavigationDestination.Staff, "2");
            Assert.AreEqual(NavigationDestination.Anime, nav.RootDestination);
        }
    }
}
