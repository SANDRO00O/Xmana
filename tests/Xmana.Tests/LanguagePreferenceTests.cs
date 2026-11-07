using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Localization;

namespace Xmana.Tests
{
    internal sealed class FakeSettingsStore : ISettingsStore
    {
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();

        public string? GetString(string key) => Values.TryGetValue(key, out var v) ? v : null;

        public void SetString(string key, string value) => Values[key] = value;
    }

    [TestClass]
    public class LanguagePreferenceTests
    {
        [TestMethod]
        public void FirstRun_UsesSupportedDeviceLanguage()
        {
            var pref = new LanguagePreference(new FakeSettingsStore());
            Assert.AreSame(LanguageCatalog.Arabic, pref.Load("ar-SA"));
        }

        [TestMethod]
        public void FirstRun_UnsupportedDeviceLanguage_FallsBackToEnglish()
        {
            var pref = new LanguagePreference(new FakeSettingsStore());
            Assert.AreSame(LanguageCatalog.English, pref.Load("fr-FR"));
            Assert.AreSame(LanguageCatalog.English, pref.Load(null));
        }

        [TestMethod]
        public void SavedLanguage_WinsOverDeviceLanguage_AndSurvivesReload()
        {
            var store = new FakeSettingsStore();
            new LanguagePreference(store).Save(LanguageCatalog.English);

            var reloaded = new LanguagePreference(store).Load("ar-SA");
            Assert.AreSame(LanguageCatalog.English, reloaded);
        }

        [TestMethod]
        public void Save_PersistsOnlyTheStableCode()
        {
            var store = new FakeSettingsStore();
            new LanguagePreference(store).Save(LanguageCatalog.Arabic);
            Assert.AreEqual("ar", store.Values[LanguagePreference.SettingsKey]);
        }

        [TestMethod]
        public void CorruptSavedValue_IsIgnored()
        {
            var store = new FakeSettingsStore();
            store.Values[LanguagePreference.SettingsKey] = "klingon";
            var pref = new LanguagePreference(store);
            Assert.AreSame(LanguageCatalog.Arabic, pref.Load("ar"));
            Assert.AreSame(LanguageCatalog.English, pref.Load("fr"));
        }
    }
}
