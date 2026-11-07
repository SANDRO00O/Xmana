using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Focus;
using Xmana.Core.Navigation;

namespace Xmana.Tests
{
    [TestClass]
    public class FocusKeysTests
    {
        [TestMethod]
        public void NavItemKeys_AreUniquePerDestination_AndRecognised()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var destination in NavigationCatalog.RailOrder)
            {
                var key = FocusKeys.NavItem(destination);
                Assert.IsTrue(FocusKeys.IsNavItem(key));
                Assert.IsTrue(seen.Add(key), "duplicate key " + key);
            }
        }

        [TestMethod]
        public void IsNavItem_RejectsContentAndNull()
        {
            Assert.IsFalse(FocusKeys.IsNavItem(FocusKeys.ContentPrimary));
            Assert.IsFalse(FocusKeys.IsNavItem(null));
            Assert.IsFalse(FocusKeys.IsNavItem(""));
        }

        [TestMethod]
        public void LanguageOptionKey_IncludesCode()
        {
            Assert.AreEqual("settings.language.ar", FocusKeys.LanguageOption("ar"));
        }
    }
}
