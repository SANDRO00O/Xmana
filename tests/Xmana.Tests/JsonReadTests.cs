using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;

namespace Xmana.Tests
{
    [TestClass]
    public class JsonReadTests
    {
        private static JToken P(string json) => JsonParser.Parse(json);

        [TestMethod]
        public void Parse_EmptyOrWhitespace_Throws()
        {
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse(""));
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse("   "));
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse(null));
        }

        [TestMethod]
        public void Parse_InvalidJson_Throws()
        {
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse("{\"a\":"));
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse("<html>nope</html>"));
        }

        [TestMethod]
        public void Parse_TrailingContent_Throws()
        {
            Assert.ThrowsException<JsonReaderException>(() => JsonParser.Parse("{} {}"));
        }

        [TestMethod]
        public void Parse_DateLikeStrings_StayStrings()
        {
            var token = P("{\"mDate\":\"2024-05-01T10:00:00\"}");
            Assert.AreEqual("2024-05-01T10:00:00", JsonRead.Text(JsonRead.Get(token, "mDate")));
        }

        [TestMethod]
        public void Parse_EmptyArrayAndObject_Succeed()
        {
            Assert.AreEqual(0, ((JArray)P("[]")).Count);
            Assert.AreEqual(0, ((JObject)P("{}")).Count);
        }

        [TestMethod]
        public void Get_IsCaseInsensitive_AndNullSafe()
        {
            var token = P("{\"En_Title\":\"x\"}");
            Assert.AreEqual("x", JsonRead.Text(JsonRead.Get(token, "en_title")));
            Assert.IsNull(JsonRead.Get(null, "a"));
            Assert.IsNull(JsonRead.Get(P("[1]"), "a"));
        }

        [DataTestMethod]
        [DataRow("{\"a\":\"42\"}", 42)]
        [DataRow("{\"a\":42}", 42)]
        [DataRow("{\"a\":42.0}", 42)]
        [DataRow("{\"a\":\" 7 \"}", 7)]
        [DataRow("{\"a\":-3}", -3)]
        public void Int_AcceptsNumbersAndNumericStrings(string json, int expected)
        {
            Assert.AreEqual(expected, JsonRead.Int(JsonRead.Get(P(json), "a")));
        }

        [DataTestMethod]
        [DataRow("{\"a\":null}")]
        [DataRow("{\"a\":\"abc\"}")]
        [DataRow("{\"a\":\"\"}")]
        [DataRow("{\"a\":1.5}")]
        [DataRow("{\"a\":[1]}")]
        [DataRow("{\"a\":{\"b\":1}}")]
        [DataRow("{\"a\":true}")]
        [DataRow("{}")]
        [DataRow("{\"a\":99999999999}")]
        public void Int_RejectsEverythingElseWithoutThrowing(string json)
        {
            Assert.IsNull(JsonRead.Int(JsonRead.Get(P(json), "a")));
        }

        [TestMethod]
        public void Double_ParsesInvariantStrings_AndRejectsGarbage()
        {
            Assert.AreEqual(7.5, JsonRead.Double(JsonRead.Get(P("{\"a\":\"7.5\"}"), "a")));
            Assert.AreEqual(7.5, JsonRead.Double(JsonRead.Get(P("{\"a\":7.5}"), "a")));
            Assert.IsNull(JsonRead.Double(JsonRead.Get(P("{\"a\":\"7,5\"}"), "a")));
            Assert.IsNull(JsonRead.Double(JsonRead.Get(P("{\"a\":\"NaN\"}"), "a")));
            Assert.IsNull(JsonRead.Double(JsonRead.Get(P("{\"a\":null}"), "a")));
        }

        [DataTestMethod]
        [DataRow("{\"a\":true}", true)]
        [DataRow("{\"a\":false}", false)]
        [DataRow("{\"a\":1}", true)]
        [DataRow("{\"a\":0}", false)]
        [DataRow("{\"a\":\"true\"}", true)]
        [DataRow("{\"a\":\"FALSE\"}", false)]
        [DataRow("{\"a\":\"1\"}", true)]
        public void Bool_AcceptsCommonRepresentations(string json, bool expected)
        {
            Assert.AreEqual(expected, JsonRead.Bool(JsonRead.Get(P(json), "a")));
        }

        [DataTestMethod]
        [DataRow("{\"a\":2}")]
        [DataRow("{\"a\":\"maybe\"}")]
        [DataRow("{\"a\":null}")]
        [DataRow("{\"a\":[]}")]
        public void Bool_RejectsUnknownValues(string json)
        {
            Assert.IsNull(JsonRead.Bool(JsonRead.Get(P(json), "a")));
        }

        [TestMethod]
        public void Text_TrimsAndTreatsBlankAsMissing()
        {
            Assert.AreEqual("hi", JsonRead.Text(JsonRead.Get(P("{\"a\":\"  hi \"}"), "a")));
            Assert.IsNull(JsonRead.Text(JsonRead.Get(P("{\"a\":\"   \"}"), "a")));
            Assert.AreEqual("5", JsonRead.Text(JsonRead.Get(P("{\"a\":5}"), "a")));
            Assert.IsNull(JsonRead.Text(JsonRead.Get(P("{\"a\":{}}"), "a")));
        }

        [TestMethod]
        public void Items_ExtractsAllDocumentedResponseStyles()
        {
            Assert.AreEqual(2, JsonItems.Extract(P("[{\"nb\":1},{\"nb\":2}]")).Count);
            Assert.AreEqual(1, JsonItems.Extract(P("{\"info\":[{\"nb\":1}]}")).Count);
            Assert.AreEqual(1, JsonItems.Extract(P("{\"status\":\"x\",\"rows\":[{\"nb\":1}]}")).Count);
        }

        [TestMethod]
        public void Items_ReturnsEmptyForUnusableShapes()
        {
            Assert.AreEqual(0, JsonItems.Extract(P("[]")).Count);
            Assert.AreEqual(0, JsonItems.Extract(P("{}")).Count);
            Assert.AreEqual(0, JsonItems.Extract(P("{\"info\":[]}")).Count);
            Assert.AreEqual(0, JsonItems.Extract(P("\"text\"")).Count);
            Assert.AreEqual(0, JsonItems.Extract(P("null")).Count);
            Assert.AreEqual(0, JsonItems.Extract(null).Count);
        }
    }
}
