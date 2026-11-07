using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;

namespace Xmana.Tests
{
    [TestClass]
    public class SearchEncodingTests
    {
        [TestMethod]
        public void Conan_EncodesToDocumentedValue()
        {
            Assert.AreEqual("Q29uYW4", SearchEncoding.EncodeKeyword("Conan"));
        }

        [TestMethod]
        public void Output_HasNoPaddingAndNoStandardBase64Characters()
        {
            foreach (var input in new[] { "a", "ab", "abc", "???>>>", "~~~~", "\u00FF\u00FE", "hello world" })
            {
                var encoded = SearchEncoding.EncodeKeyword(input);
                Assert.IsFalse(encoded.Contains("="), input);
                Assert.IsFalse(encoded.Contains("+"), input);
                Assert.IsFalse(encoded.Contains("/"), input);
            }
        }

        [TestMethod]
        public void UrlSafeAlphabet_IsUsed()
        {
            // "???>>>" is the classic input whose standard Base64 is "Pz8/Pj4+".
            Assert.AreEqual("Pz8_Pj4-", SearchEncoding.EncodeKeyword("???>>>"));
        }

        [TestMethod]
        public void PaddingLengths_AreAllHandled()
        {
            Assert.AreEqual("YQ", SearchEncoding.EncodeKeyword("a"));
            Assert.AreEqual("YWI", SearchEncoding.EncodeKeyword("ab"));
            Assert.AreEqual("YWJj", SearchEncoding.EncodeKeyword("abc"));
        }

        [DataTestMethod]
        [DataRow("Conan")]
        [DataRow("Lupin III vs. Detective Conan")]
        [DataRow("100% & more?")]
        [DataRow("a/b\\c:d")]
        [DataRow("المحقق كونان")]
        [DataRow("日本語のタイトル")]
        [DataRow("emoji \U0001F600 test")]
        [DataRow("  spaces  ")]
        public void RoundTrips_SpecialCharactersAndNonLatinText(string keyword)
        {
            Assert.AreEqual(keyword, SearchEncoding.DecodeKeyword(SearchEncoding.EncodeKeyword(keyword)));
        }

        [TestMethod]
        public void EmptyString_EncodesToEmpty()
        {
            Assert.AreEqual(string.Empty, SearchEncoding.EncodeKeyword(string.Empty));
        }

        [TestMethod]
        public void Null_Throws()
        {
            Assert.ThrowsException<System.ArgumentNullException>(() => SearchEncoding.EncodeKeyword(null!));
        }
    }
}
