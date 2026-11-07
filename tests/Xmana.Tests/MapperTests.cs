using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Tests
{
    [TestClass]
    public class VideoMapperTests
    {
        private static JToken P(string json) => JsonParser.Parse(json);

        [TestMethod]
        public void Details_MapsDocumentedFields_FromStringTypedNumbers()
        {
            var details = VideoMapper.ToDetails(P(Json.Video));

            Assert.IsNotNull(details);
            Assert.AreEqual("1119122", details!.Id);
            Assert.AreEqual("Detective Conan", details.Summary.EnTitle);
            Assert.AreEqual("المحقق كونان", details.Summary.ArTitle);
            Assert.AreEqual(7.5, details.Summary.Stars);
            Assert.AreEqual("1996", details.Summary.Year);
            Assert.AreEqual(VideoKind.Series, details.Summary.Kind);
            Assert.AreEqual("2", details.Summary.KindRaw);
            Assert.AreEqual(3, details.Summary.Season);
            Assert.AreEqual(12, details.Summary.EpisodeNumber);
            Assert.AreEqual("1119119", details.Summary.RootSeriesId);
            Assert.AreEqual(true, details.HasIntroSkipping);
            Assert.AreEqual(true, details.ShowComments);
            Assert.AreEqual(4, details.CommentsCount);
            Assert.AreEqual(12345L, details.ViewsCount);
            Assert.AreEqual("1500", details.Duration);
            Assert.AreEqual(2, details.Summary.Categories.Count);
            Assert.IsNull(details.Summary.Categories[1].ArTitle);
            Assert.AreEqual(1, details.Actors.Count);
            Assert.AreEqual("Minami Takayama", details.Actors[0].Name);
            Assert.AreEqual("77", details.Actors[0].Id);
            Assert.AreEqual(0, details.Directors.Count);
            Assert.AreEqual(0, details.Writers.Count);
        }

        [TestMethod]
        public void Details_PreservesRawObject_AndSignedUrlFieldsForDisplay()
        {
            var details = VideoMapper.ToDetails(P(Json.Video))!;
            Assert.AreEqual("1119122", (string?)details.Summary.Raw["nb"]);
            Assert.IsNotNull(details.Summary.ImageObjUrl);
            Assert.IsNotNull(details.Summary.MediumThumbObjUrl);
            Assert.IsNotNull(details.VideoLanguages);
        }

        [TestMethod]
        public void Details_MissingFieldsBecomeNull_NotExceptions()
        {
            var details = VideoMapper.ToDetails(P("{\"nb\":\"5\"}"))!;
            Assert.AreEqual("5", details.Id);
            Assert.IsNull(details.Summary.EnTitle);
            Assert.IsNull(details.Summary.Stars);
            Assert.IsNull(details.HasIntroSkipping);
            Assert.IsNull(details.ViewsCount);
            Assert.AreEqual(VideoKind.Unknown, details.Summary.Kind);
            Assert.AreEqual(0, details.Summary.Categories.Count);
            Assert.AreEqual(0, details.Actors.Count);
        }

        [TestMethod]
        public void Details_WrongTypesAndNulls_AreTolerated()
        {
            var json = @"{ ""nb"": 99, ""en_title"": null, ""stars"": ""n/a"", ""season"": [], ""episodeNummer"": {""x"":1},
                           ""categories"": ""oops"", ""actorsInfo"": [ 1, ""x"", null, {""name"":""Real""} ], ""hasIntroSkipping"": ""maybe"",
                           ""videoViewsNumber"": ""lots"", ""kind"": 7 }";
            var details = VideoMapper.ToDetails(P(json))!;

            Assert.AreEqual("99", details.Id);
            Assert.IsNull(details.Summary.EnTitle);
            Assert.IsNull(details.Summary.Stars);
            Assert.IsNull(details.Summary.Season);
            Assert.IsNull(details.Summary.EpisodeNumber);
            Assert.AreEqual(0, details.Summary.Categories.Count);
            Assert.AreEqual(1, details.Actors.Count);
            Assert.AreEqual("Real", details.Actors[0].Name);
            Assert.IsNull(details.HasIntroSkipping);
            Assert.IsNull(details.ViewsCount);
            Assert.AreEqual(VideoKind.Unknown, details.Summary.Kind);
            Assert.AreEqual("7", details.Summary.KindRaw);
        }

        [DataTestMethod]
        [DataRow("{}")]
        [DataRow("{\"en_title\":\"no id\"}")]
        [DataRow("{\"nb\":\"\"}")]
        [DataRow("{\"nb\":null}")]
        [DataRow("[]")]
        [DataRow("null")]
        [DataRow("\"text\"")]
        [DataRow("42")]
        public void Details_WithoutUsableId_IsNull(string json)
        {
            Assert.IsNull(VideoMapper.ToDetails(P(json)));
            Assert.IsNull(VideoMapper.ToSummary(P(json)));
        }

        [TestMethod]
        public void Details_AcceptsAWrappedSingleObject()
        {
            Assert.AreEqual("5", VideoMapper.ToDetails(P("[{\"nb\":\"5\"}]"))!.Id);
            Assert.AreEqual("5", VideoMapper.ToDetails(P("{\"info\":[{\"nb\":\"5\"}]}"))!.Id);
        }

        [TestMethod]
        public void Summaries_SkipInvalidItems_AndDuplicateIds()
        {
            var items = JsonItems.Extract(P("[{\"nb\":\"1\"},{\"x\":1},null,\"s\",{\"nb\":\"1\"},{\"nb\":\"2\"}]"));
            var list = VideoMapper.ToSummaries(items);
            CollectionAssert.AreEqual(new[] { "1", "2" }, list.Select(v => v.Id).ToArray());
        }

        [DataTestMethod]
        [DataRow("1", VideoKind.Movie)]
        [DataRow("2", VideoKind.Series)]
        [DataRow("3", VideoKind.Unknown)]
        [DataRow("", VideoKind.Unknown)]
        [DataRow(null, VideoKind.Unknown)]
        public void Kind_MapsOnlyDocumentedValues(string? raw, VideoKind expected)
        {
            Assert.AreEqual(expected, VideoMapper.ParseKind(raw));
        }

        [TestMethod]
        public void TitlePreference_FallsBackToTheOtherLanguage()
        {
            var both = VideoMapper.ToSummary(P("{\"nb\":\"1\",\"en_title\":\"E\",\"ar_title\":\"ع\"}"))!;
            Assert.AreEqual("ع", both.GetTitle(true));
            Assert.AreEqual("E", both.GetTitle(false));

            var onlyEnglish = VideoMapper.ToSummary(P("{\"nb\":\"1\",\"en_title\":\"E\",\"ar_title\":\"  \"}"))!;
            Assert.AreEqual("E", onlyEnglish.GetTitle(true));

            var none = VideoMapper.ToSummary(P("{\"nb\":\"1\"}"))!;
            Assert.IsNull(none.GetTitle(true));
        }

        [TestMethod]
        public void RootSeries_IsPreservedExactlyAsSent()
        {
            Assert.AreEqual("0", VideoMapper.ToSummary(P("{\"nb\":\"1\",\"rootSeries\":\"0\"}"))!.RootSeriesId);
            Assert.IsNull(VideoMapper.ToSummary(P("{\"nb\":\"1\"}"))!.RootSeriesId);
        }
    }

    [TestClass]
    public class CatalogMapperTests
    {
        private static JToken P(string json) => JsonParser.Parse(json);

        [TestMethod]
        public void Groups_MapDocumentedFields()
        {
            var json = @"{ ""groups"": [
                { ""groupsID"": ""404"", ""title"": ""Top"", ""hasPagination"": ""1"", ""content"": [ {""nb"":""1""}, {""nb"":""2""} ] },
                { ""groupsID"": 405, ""title"": ""Empty"", ""hasPagination"": false, ""content"": [] },
                { ""title"": ""no id"" } ] }";
            var groups = CatalogMapper.ToGroups(P(json));

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("404", groups[0].GroupId);
            Assert.AreEqual("Top", groups[0].Title);
            Assert.AreEqual(true, groups[0].HasPagination);
            Assert.AreEqual(2, groups[0].Items.Count);
            Assert.AreEqual("405", groups[1].GroupId);
            Assert.AreEqual(false, groups[1].HasPagination);
            Assert.AreEqual(0, groups[1].Items.Count);
        }

        [DataTestMethod]
        [DataRow("{\"groups\":[]}")]
        [DataRow("{}")]
        [DataRow("[]")]
        [DataRow("null")]
        [DataRow("{\"groups\":\"x\"}")]
        public void Groups_EmptyOrOddShapes_GiveEmptyList(string json)
        {
            Assert.AreEqual(0, CatalogMapper.ToGroups(P(json)).Count);
        }

        [TestMethod]
        public void Categories_MapTitles_AndTolerateMissingId()
        {
            var main = CatalogMapper.ToCategories(P("[{\"nb\":\"84\",\"en_title\":\"Action\",\"ar_title\":\"أكشن\"},{\"en_title\":\"NoId\"},{\"foo\":1},5]"), true);
            Assert.AreEqual(2, main.Count);
            Assert.AreEqual("84", main[0].Id);
            Assert.AreEqual("Action", main[0].EnTitle);
            Assert.IsTrue(main[0].IsMain);
            Assert.IsNull(main[1].Id);
            Assert.IsFalse(CatalogMapper.ToCategories(P("[{\"en_title\":\"x\"}]"), false)[0].IsMain);
        }

        [TestMethod]
        public void CategoryLanguageGroups_MapTheVerifiedLangArrayShape()
        {
            var json = @"[
              { ""nb"": ""84"", ""en_title"": ""Action"", ""ar_title"": ""أكشن"", ""langArray"": [
                  { ""catNb"": ""84"", ""langNb"": ""22"", ""langFreq"": ""120"", ""langFreqSeries"": 7, ""lang_ar_title"": ""اليابانية"", ""lang_en_title"": ""Japanese"" },
                  { ""catNb"": ""84"", ""langNb"": ""22"", ""langFreq"": ""1"" },
                  { ""catNb"": ""84"", ""langFreq"": ""5"" },
                  { ""catNb"": ""84"", ""langNb"": ""7"", ""langFreq"": ""x"" } ] },
              { ""nb"": ""57"", ""en_title"": ""Animation"" },
              { ""en_title"": ""no id"" } ]";
            var groups = CatalogMapper.ToCategoryLanguageGroups(P(json));

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("84", groups[0].CategoryId);
            Assert.AreEqual("Action", groups[0].EnTitle);
            Assert.AreEqual(2, groups[0].Languages.Count, "duplicate and id-less language entries are dropped");
            Assert.AreEqual("22", groups[0].Languages[0].LanguageId);
            Assert.AreEqual(120, groups[0].Languages[0].MovieCount);
            Assert.AreEqual(7, groups[0].Languages[0].SeriesCount);
            Assert.AreEqual("Japanese", groups[0].Languages[0].EnTitle);
            Assert.AreEqual("اليابانية", groups[0].Languages[0].GetTitle(true));
            Assert.IsNull(groups[0].Languages[1].MovieCount);
            Assert.AreEqual(0, groups[1].Languages.Count);
        }

        [TestMethod]
        public void CategoryLanguageGroups_EmptyOrOddShapes_GiveEmptyList()
        {
            Assert.AreEqual(0, CatalogMapper.ToCategoryLanguageGroups(P("[]")).Count);
            Assert.AreEqual(0, CatalogMapper.ToCategoryLanguageGroups(P("{}")).Count);
            Assert.AreEqual(0, CatalogMapper.ToCategoryLanguageGroups(null).Count);
        }

        [TestMethod]
        public void Seasons_AreSortedAndDeduplicated_FromObjectsOrBareNumbers()
        {
            var fromObjects = CatalogMapper.ToSeasons(P("[{\"season\":\"3\"},{\"season\":\"1\"},{\"season\":\"3\"},{\"season\":\"x\"}]"));
            CollectionAssert.AreEqual(new[] { 1, 3 }, fromObjects.Select(s => s.Number).ToArray());

            var fromNumbers = CatalogMapper.ToSeasons(P("[2,1]"));
            CollectionAssert.AreEqual(new[] { 1, 2 }, fromNumbers.Select(s => s.Number).ToArray());

            Assert.AreEqual(0, CatalogMapper.ToSeasons(P("[]")).Count);
        }

        [TestMethod]
        public void GroupBySeason_DoesNotTrustArrayOrder()
        {
            var json = @"[
              {""nb"":""e3"",""season"":""2"",""episodeNummer"":""1""},
              {""nb"":""e2"",""season"":""1"",""episodeNummer"":""2""},
              {""nb"":""e1"",""season"":""1"",""episodeNummer"":""1""},
              {""nb"":""e4"",""season"":""2"",""episodeNummer"":""10""},
              {""nb"":""e5"",""season"":""2"",""episodeNummer"":""2""},
              {""nb"":""x1"",""episodeNummer"":""1""},
              {""nb"":""n1"",""season"":""1""}
            ]";
            var episodes = VideoMapper.ToSummaries(JsonItems.Extract(P(json)));
            var groups = CatalogMapper.GroupBySeason(episodes);

            Assert.AreEqual(3, groups.Count);
            Assert.AreEqual(1, groups[0].Season);
            CollectionAssert.AreEqual(new[] { "e1", "e2", "n1" }, groups[0].Episodes.Select(e => e.Id).ToArray());
            Assert.AreEqual(2, groups[1].Season);
            CollectionAssert.AreEqual(new[] { "e3", "e5", "e4" }, groups[1].Episodes.Select(e => e.Id).ToArray());
            Assert.IsNull(groups[2].Season);
            CollectionAssert.AreEqual(new[] { "x1" }, groups[2].Episodes.Select(e => e.Id).ToArray());
        }

        [TestMethod]
        public void GroupBySeason_EmptyInput_GivesNoGroups()
        {
            Assert.AreEqual(0, CatalogMapper.GroupBySeason(new VideoSummary[0]).Count);
        }

        [TestMethod]
        public void StaffPage_FindsStaffObjectAtRootOrNested_AndTitles()
        {
            var nested = CatalogMapper.ToStaffPage(P("{\"staff\":{\"nb\":\"77\",\"name\":\"Minami Takayama\",\"role\":\"Actor\"},\"info\":[{\"nb\":\"1\"},{\"nb\":\"2\"}]}"));
            Assert.AreEqual("Minami Takayama", nested.Staff!.Name);
            Assert.AreEqual(2, nested.Titles.Count);

            var root = CatalogMapper.ToStaffPage(P("{\"nb\":\"77\",\"name\":\"X\",\"staff_img\":\"https://e.invalid/a.jpg\"}"));
            Assert.AreEqual("X", root.Staff!.Name);
            Assert.AreEqual(0, root.Titles.Count);

            var neither = CatalogMapper.ToStaffPage(P("[]"));
            Assert.IsNull(neither.Staff);
            Assert.AreEqual(0, neither.Titles.Count);
        }

        [TestMethod]
        public void Faq_ExposesRawEntries_AndHandlesEmpty()
        {
            Assert.AreEqual(2, CatalogMapper.ToFaq(P("[{\"q\":1},{\"q\":2},3]")).Count);
            Assert.AreEqual(0, CatalogMapper.ToFaq(P("[]")).Count);
            Assert.AreEqual(0, CatalogMapper.ToFaq(P("{}")).Count);
        }
    }

    [TestClass]
    public class PlaybackMapperTests
    {
        private static JToken P(string json) => JsonParser.Parse(json);

        [TestMethod]
        public void Qualities_DetectedFromLabelAndUrlValues_SortedByHeight()
        {
            var json = @"[
              { ""resolution"": ""720p"", ""videoUrl"": ""https://cdn.invalid/720.mp4?sig=A"" },
              { ""resolution"": ""240p"", ""videoUrl"": ""https://cdn.invalid/240.mp4?sig=B"" },
              { ""resolution"": ""1080p"", ""videoUrl"": ""https://cdn.invalid/1080.mp4?sig=C"" } ]";
            var q = PlaybackMapper.ToQualities(P(json));

            CollectionAssert.AreEqual(new[] { "240p", "720p", "1080p" }, q.Select(x => x.Label).ToArray());
            CollectionAssert.AreEqual(new[] { 240, 720, 1080 }, q.Select(x => x.Height).ToArray());
        }

        [TestMethod]
        public void Qualities_DetectedFromKeyedObject()
        {
            var q = PlaybackMapper.ToQualities(P("{\"360p\":\"https://cdn.invalid/a.mp4\",\"480p\":{\"url\":\"https://cdn.invalid/b.mp4\"}}"));
            CollectionAssert.AreEqual(new[] { 360, 480 }, q.Select(x => x.Height).ToArray());
        }

        [TestMethod]
        public void Qualities_NothingIsAssumed_WhenDataIsMissingOrInsecure()
        {
            Assert.AreEqual(0, PlaybackMapper.ToQualities(P("[]")).Count);
            Assert.AreEqual(0, PlaybackMapper.ToQualities(P("{}")).Count);
            Assert.AreEqual(0, PlaybackMapper.ToQualities(null).Count);
            Assert.AreEqual(0, PlaybackMapper.ToQualities(P("[{\"resolution\":\"720p\"}]")).Count);
            Assert.AreEqual(0, PlaybackMapper.ToQualities(P("[{\"resolution\":\"720p\",\"videoUrl\":\"http://insecure.invalid/a.mp4\"}]")).Count);
            Assert.AreEqual(0, PlaybackMapper.ToQualities(P("[{\"resolution\":\"high\",\"videoUrl\":\"https://cdn.invalid/a.mp4\"}]")).Count);
        }

        [TestMethod]
        public void Quality_ToString_NeverRevealsTheSignedUrl()
        {
            var quality = PlaybackMapper.ToQualities(P("[{\"resolution\":\"720p\",\"videoUrl\":\"https://cdn.invalid/a.mp4?sig=SECRET\"}]")).Single();
            Assert.AreEqual("720p", quality.ToString());
            Assert.IsFalse(quality.ToString().Contains("SECRET"));
        }

        [TestMethod]
        public void Translations_OnlyNonEmptyFilesCountAsAvailable()
        {
            var empty = PlaybackMapper.ToTranslations(P(@"{ ""arTranslationFile"": """", ""enTranslationFile"": """", ""spTranslationFile"": """",
                ""arTranslationFilePath"": ""defaultImages/loading.gif"", ""enTranslationFilePath"": ""defaultImages/loading.gif"" }"));
            Assert.IsFalse(empty.HasSubtitles);
            Assert.AreEqual(0, empty.Tracks.Count);
            Assert.AreEqual("defaultImages/loading.gif", empty.ArPath);

            var some = PlaybackMapper.ToTranslations(P("{\"arTranslationFile\":\"a.srt\",\"enTranslationFile\":\" \",\"spTranslationFile\":\"s.srt\"}"));
            CollectionAssert.AreEqual(new[] { SubtitleLanguage.Arabic, SubtitleLanguage.Spanish }, some.Tracks.Select(t => t.Language).ToArray());
            Assert.IsTrue(some.HasSubtitles);
        }

        [TestMethod]
        public void Translations_NonObjectResponse_IsHandled()
        {
            Assert.IsFalse(PlaybackMapper.ToTranslations(P("[]")).HasSubtitles);
        }

        [TestMethod]
        public void SkipInfo_EmptyArrays_MeansNoSkipButton()
        {
            var info = PlaybackMapper.ToSkipInfo(P("{\"start\":[],\"end\":[]}"));
            Assert.IsFalse(info.HasUsableRanges);
            Assert.AreEqual(0, info.StartCount);
        }

        [TestMethod]
        public void SkipInfo_OnlyCompleteOrderedPairsAreUsed_NothingIsInvented()
        {
            var info = PlaybackMapper.ToSkipInfo(P("{\"start\":[\"10\",20,\"x\",50,60],\"end\":[40,15,100,50]}"));
            Assert.AreEqual(5, info.StartCount);
            Assert.AreEqual(4, info.EndCount);
            Assert.AreEqual(1, info.Ranges.Count);
            Assert.AreEqual(10.0, info.Ranges[0].Start);
            Assert.AreEqual(40.0, info.Ranges[0].End);
        }

        [TestMethod]
        public void SkipInfo_MissingOrWrongShape_IsEmpty()
        {
            Assert.IsFalse(PlaybackMapper.ToSkipInfo(P("{}")).HasUsableRanges);
            Assert.IsFalse(PlaybackMapper.ToSkipInfo(P("[]")).HasUsableRanges);
            Assert.IsFalse(PlaybackMapper.ToSkipInfo(P("{\"start\":5,\"end\":\"x\"}")).HasUsableRanges);
        }

        [TestMethod]
        public void Thumbnails_Status204AndEmpty_IsAGracefulResult()
        {
            var info = PlaybackMapper.ToThumbnails(P("{\"videoThumbnails\":[],\"status\":204}"));
            Assert.AreEqual(204, info.Status);
            Assert.IsFalse(info.HasThumbnails);
            Assert.AreEqual(0, info.ItemCount);
        }

        [TestMethod]
        public void Thumbnails_KeepOnlyHttpsUrls()
        {
            var info = PlaybackMapper.ToThumbnails(P("{\"videoThumbnails\":[\"https://e.invalid/1.jpg\",{\"url\":\"https://e.invalid/2.jpg\"},\"http://e.invalid/3.jpg\",7],\"status\":200}"));
            Assert.AreEqual(2, info.Urls.Count);
            Assert.AreEqual(4, info.ItemCount);
            Assert.IsTrue(info.HasThumbnails);
        }

        [TestMethod]
        public void Thumbnails_MissingFields_AreHandled()
        {
            var info = PlaybackMapper.ToThumbnails(P("{}"));
            Assert.IsNull(info.Status);
            Assert.IsFalse(info.HasThumbnails);
        }

        [TestMethod]
        public void ParentalStatus_IsPreservedExactly_WithNoInterpretation()
        {
            Assert.AreEqual("1", PlaybackMapper.ToParentalStatus(P("{\"status\":\"1\"}")).Status);
            Assert.AreEqual("7", PlaybackMapper.ToParentalStatus(P("{\"status\":7}")).Status);
            Assert.IsNull(PlaybackMapper.ToParentalStatus(P("{}")).Status);
            Assert.IsNull(PlaybackMapper.ToParentalStatus(P("[]")).Status);
        }

        [TestMethod]
        public void Comments_EmptyList_IsAnEmptyResult_AndObjectsAreKeptRaw()
        {
            Assert.AreEqual(0, PlaybackMapper.ToComments(P("[]")).Count);
            var comments = PlaybackMapper.ToComments(P("[{\"id\":1,\"text\":\"hi\"},5,null]"));
            Assert.AreEqual(1, comments.Count);
            Assert.AreEqual("hi", (string?)comments[0].Raw["text"]);
        }

        [TestMethod]
        public void RatingSummary_ShowsDataOnlyWhenPresent()
        {
            var none = PlaybackMapper.ToRatingSummary(P("{\"items\":[],\"totalCount\":0}"));
            Assert.IsFalse(none.HasData);
            Assert.AreEqual(0, none.TotalCount);

            var some = PlaybackMapper.ToRatingSummary(P("{\"items\":[{\"average\":4.5}],\"totalCount\":1}"));
            Assert.IsTrue(some.HasData);

            Assert.IsFalse(PlaybackMapper.ToRatingSummary(P("{}")).HasData);
            Assert.IsFalse(PlaybackMapper.ToRatingSummary(P("[]")).HasData);
        }
    }
}
