using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Localization;

namespace Xmana.Tests
{
    [TestClass]
    public class LanguageCatalogTests
    {
        [TestMethod]
        public void Catalog_ContainsExactlyArabicAndEnglish()
        {
            CollectionAssert.AreEquivalent(new[] { "ar", "en" }, System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(LanguageCatalog.All, l => l.Code)));
        }

        [TestMethod]
        public void Arabic_IsRightToLeft_English_IsLeftToRight()
        {
            Assert.IsTrue(LanguageCatalog.Arabic.IsRightToLeft);
            Assert.IsFalse(LanguageCatalog.English.IsRightToLeft);
        }

        [TestMethod]
        public void NativeNames_AreInOwnScript()
        {
            Assert.AreEqual("English", LanguageCatalog.English.NativeName);
            Assert.AreEqual("\u0627\u0644\u0639\u0631\u0628\u064A\u0629", LanguageCatalog.Arabic.NativeName);
        }

        [DataTestMethod]
        [DataRow("ar", "ar")]
        [DataRow("AR", "ar")]
        [DataRow("ar-SA", "ar")]
        [DataRow("ar_EG", "ar")]
        [DataRow("en-US", "en")]
        [DataRow("en-GB", "en")]
        [DataRow(" en ", "en")]
        public void TryResolveTag_MapsPrimarySubtag(string tag, string expectedCode)
        {
            Assert.IsTrue(LanguageCatalog.TryResolveTag(tag, out var language));
            Assert.AreEqual(expectedCode, language.Code);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("fr-FR")]
        [DataRow("ja")]
        public void TryResolveTag_UnsupportedOrEmpty_ReturnsFalseAndDefault(string? tag)
        {
            Assert.IsFalse(LanguageCatalog.TryResolveTag(tag, out var language));
            Assert.AreSame(LanguageCatalog.Default, language);
        }

        [TestMethod]
        public void TryGetByCode_IsCaseInsensitive_AndRejectsUnknown()
        {
            Assert.IsTrue(LanguageCatalog.TryGetByCode("Ar", out var ar));
            Assert.AreSame(LanguageCatalog.Arabic, ar);
            Assert.IsFalse(LanguageCatalog.TryGetByCode("xx", out _));
            Assert.IsFalse(LanguageCatalog.TryGetByCode(null, out _));
        }
    }
}
