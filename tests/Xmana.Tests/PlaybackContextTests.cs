using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Caching;
using Xmana.Core.Http;
using Xmana.Core.Json;
using Xmana.Core.Models;
using Xmana.Core.Playback;

namespace Xmana.Tests
{
    [TestClass]
    public class PlaybackContextLoaderTests
    {
        private static VideoDetails Info(string json) => VideoMapper.ToDetails(JsonParser.Parse(json))!;

        [TestMethod]
        public async Task Movie_HasTitlesAndNoSeriesData_AndMakesNoSeriesRequests()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(Info("{\"nb\":\"192319\",\"en_title\":\"Movie\",\"ar_title\":\"فيلم\",\"kind\":\"1\",\"rootSeries\":\"0\",\"hasIntroSkipping\":\"false\"}"), DataOrigin.Network, null)),
            };
            var context = await new PlaybackContextLoader(source).LoadAsync("192319", CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.IsFalse(context!.IsEpisode);
            Assert.AreEqual("فيلم", context.GetTitle(true));
            Assert.AreEqual("Movie", context.GetTitle(false));
            Assert.IsNull(context.Next);
            Assert.AreEqual(false, context.HasIntroSkipping);
            Assert.AreEqual(0, source.Calls.Count(c => c.StartsWith("episodes")));
        }

        [TestMethod]
        public async Task Episode_UsesTheRootSeries_ForPreviousNextAndTheSeasonList()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(Info("{\"nb\":\"1119122\",\"en_title\":\"Ep 2\",\"kind\":\"2\",\"rootSeries\":\"1119119\",\"season\":\"1\",\"episodeNummer\":\"2\",\"hasIntroSkipping\":\"true\"}"), DataOrigin.Network, null)),
                Episodes = id => FakeDetailsSource.EpisodesOf("1119123/1/3", "1119121/1/1", "1119122/1/2", "1119200/2/1"),
            };
            var context = await new PlaybackContextLoader(source).LoadAsync("1119122", CancellationToken.None);

            Assert.IsTrue(source.Calls.Contains("episodes:1119119"));
            Assert.IsFalse(source.Calls.Contains("episodes:1119122"));
            Assert.IsTrue(context!.IsEpisode);
            Assert.AreEqual("1119119", context.SeriesId);
            Assert.AreEqual(1, context.Season);
            Assert.AreEqual(2, context.Episode);
            Assert.AreEqual("1119121", context.Previous!.Id);
            Assert.AreEqual("1119123", context.Next!.Id);
            CollectionAssert.AreEqual(new[] { "1119121", "1119122", "1119123" }, context.SeasonEpisodes.Select(e => e.Id).ToArray());
            Assert.AreEqual(true, context.HasIntroSkipping);
        }

        [TestMethod]
        public async Task TheLastEpisodeOfASeason_NextCrossesIntoTheNextSeason()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(Info("{\"nb\":\"e13\",\"kind\":\"2\",\"rootSeries\":\"S\",\"season\":\"1\",\"episodeNummer\":\"3\"}"), DataOrigin.Network, null)),
                Episodes = id => FakeDetailsSource.EpisodesOf("e11/1/1", "e12/1/2", "e13/1/3", "e21/2/1"),
            };
            var context = await new PlaybackContextLoader(source).LoadAsync("e13", CancellationToken.None);
            Assert.AreEqual("e21", context!.Next!.Id);
        }

        [TestMethod]
        public async Task EpisodeListFailure_OnlyRemovesTheSeriesConveniences()
        {
            var source = new FakeDetailsSource
            {
                Info = id => Task.FromResult(new ApiResult<VideoDetails?>(Info("{\"nb\":\"1119122\",\"kind\":\"2\",\"rootSeries\":\"1119119\"}"), DataOrigin.Network, null)),
                Episodes = id => throw FakeHomeSource.Fail(),
            };
            var context = await new PlaybackContextLoader(source).LoadAsync("1119122", CancellationToken.None);
            Assert.IsNotNull(context);
            Assert.IsTrue(context!.IsEpisode);
            Assert.IsNull(context.Next);
            Assert.IsNull(context.Previous);
            Assert.AreEqual(0, context.SeasonEpisodes.Count);
        }

        [TestMethod]
        public async Task UnknownVideo_IsNull_AndAMetadataFailureIsAnApiException()
        {
            var missing = new FakeDetailsSource { Info = id => Task.FromResult(new ApiResult<VideoDetails?>(null, DataOrigin.Network, null)) };
            Assert.IsNull(await new PlaybackContextLoader(missing).LoadAsync("1", CancellationToken.None));

            var failing = new FakeDetailsSource { Info = id => throw FakeHomeSource.Fail() };
            await Assert.ThrowsExceptionAsync<ApiException>(() => new PlaybackContextLoader(failing).LoadAsync("1", CancellationToken.None));
        }

        [TestMethod]
        public void SubtitleTrack_AddressIsUsableOnlyWhenItIsAnAbsoluteHttpsUrl()
        {
            Uri uri;
            Assert.IsTrue(new SubtitleTrack(SubtitleLanguage.Arabic, "https://subs.invalid/a.srt").TryGetUri(out uri));
            Assert.AreEqual("subs.invalid", uri.Host);
            Assert.IsFalse(new SubtitleTrack(SubtitleLanguage.Arabic, "defaultImages/loading.gif").TryGetUri(out uri));
            Assert.IsFalse(new SubtitleTrack(SubtitleLanguage.Arabic, "http://subs.invalid/a.srt").TryGetUri(out uri));
            Assert.AreEqual("Arabic", new SubtitleTrack(SubtitleLanguage.Arabic, "https://subs.invalid/a.srt?sig=SECRET").ToString());
        }
    }
}
