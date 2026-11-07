using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using Xmana.Core.Api;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Json;

namespace Xmana.Tests
{
    [TestClass]
    public class RatingTargetTests
    {
        [DataTestMethod]
        [DataRow("1", "0", "192319", "192319", "Movie")]
        [DataRow("1", null, "192319", "192319", "Movie")]
        [DataRow("1", "", "192319", "192319", "Movie")]
        [DataRow("3", "0", "5", "5", "Movie")]
        [DataRow(null, "0", "5", "5", "Movie")]
        [DataRow("2", "1119119", "1119122", "1119119", "Episode")]
        [DataRow("2", "0", "1119119", "1119119", "Episode")]
        [DataRow("1", "1119119", "1119122", "1119119", "Movie")]
        [DataRow(" 2 ", " 77 ", "5", "77", "Episode")]
        public void Mapping_FollowsTheDocumentedFrontendLogic(string? kind, string? rootSeries, string nb, string expectedId, string expectedType)
        {
            var target = RatingTarget.For(kind, rootSeries, nb);
            Assert.AreEqual(expectedId, target.TargetId);
            Assert.AreEqual(expectedType, target.TargetTypeName);
        }

        [TestMethod]
        public void FromVideoSummary_UsesKindRootSeriesAndId()
        {
            var episode = VideoMapper.ToSummary(JsonParser.Parse("{\"nb\":\"1119122\",\"kind\":\"2\",\"rootSeries\":\"1119119\"}"))!;
            var movie = VideoMapper.ToSummary(JsonParser.Parse("{\"nb\":\"192319\",\"kind\":\"1\",\"rootSeries\":\"0\"}"))!;
            Assert.AreEqual("1119119", RatingTarget.For(episode).TargetId);
            Assert.AreEqual(RatingTargetType.Episode, RatingTarget.For(episode).TargetType);
            Assert.AreEqual("192319", RatingTarget.For(movie).TargetId);
            Assert.AreEqual(RatingTargetType.Movie, RatingTarget.For(movie).TargetType);
        }

        [TestMethod]
        public void BlankId_IsRejected()
        {
            Assert.ThrowsException<System.ArgumentException>(() => new RatingTarget(" ", RatingTargetType.Movie));
            Assert.ThrowsException<System.ArgumentException>(() => RatingTarget.For("1", "0", ""));
        }
    }

    [TestClass]
    public class RecommendationMapperTests
    {
        private static JToken P(string json) => JsonParser.Parse(json);

        [TestMethod]
        public void ItemsAreFoundInBareLists_InfoEnvelopes_AndNamedLists()
        {
            Assert.AreEqual(2, ExternalServiceMapper.ToRecommendations(P("[{\"nb\":\"1\"},{\"nb\":\"2\"}]")).Items.Count);
            Assert.AreEqual(1, ExternalServiceMapper.ToRecommendations(P("{\"info\":[{\"nb\":\"1\"}]}")).Items.Count);
            Assert.AreEqual(1, ExternalServiceMapper.ToRecommendations(P("{\"expirationDate\":\"x\",\"results\":[{\"MovieId\":\"9\"}]}")).Items.Count);
        }

        [TestMethod]
        public void IdAndTitle_AreReadDefensively_AndVideoObjectsBecomeSummaries()
        {
            var result = ExternalServiceMapper.ToRecommendations(P("[{\"nb\":\"1\",\"en_title\":\"One\",\"kind\":\"1\"},{\"MovieId\":\"2\",\"MovieName\":\"Two\"}]"));
            Assert.AreEqual("One", result.Items[0].Title);
            Assert.IsNotNull(result.Items[0].Video);
            Assert.AreEqual("2", result.Items[1].Id);
            Assert.AreEqual("Two", result.Items[1].Title);
            Assert.IsNull(result.Items[1].Video);
        }

        [TestMethod]
        public void ItemsWithoutAnId_AreDropped_AndDuplicatesRemoved()
        {
            var result = ExternalServiceMapper.ToRecommendations(P("[{\"x\":1},5,null,{\"nb\":\"1\"},{\"nb\":\"1\"}]"));
            CollectionAssert.AreEqual(new[] { "1" }, result.Items.Select(i => i.Id).ToArray());
        }

        [TestMethod]
        public void ExpirationDate_IsKeptAsSent()
        {
            Assert.AreEqual("2026-10-03T00:00:00Z", ExternalServiceMapper.ToRecommendations(P("{\"expirationDate\":\"2026-10-03T00:00:00Z\",\"items\":[]}")).ExpirationDate);
            Assert.IsNull(ExternalServiceMapper.ToRecommendations(P("[]")).ExpirationDate);
        }

        [DataTestMethod]
        [DataRow("[]")]
        [DataRow("{}")]
        [DataRow("null")]
        [DataRow("\"text\"")]
        [DataRow("42")]
        public void EmptyOrUnknownShapes_AreEmptyResults(string json)
        {
            Assert.IsFalse(ExternalServiceMapper.ToRecommendations(P(json)).HasItems);
        }

        [TestMethod]
        public void NullToken_IsHandled()
        {
            Assert.IsFalse(ExternalServiceMapper.ToRecommendations(null).HasItems);
        }

        [TestMethod]
        public void ItemToString_NeverPrintsRawContent()
        {
            var item = ExternalServiceMapper.ToRecommendations(P("[{\"nb\":\"1\",\"u\":\"https://signed.invalid/x?sig=SECRET\"}]")).Items.Single();
            Assert.AreEqual("1", item.ToString());
        }
    }
}
