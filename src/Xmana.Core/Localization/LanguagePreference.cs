namespace Xmana.Core.Localization
{
    /// <summary>
    /// Persists and restores the selected UI language locally. No account or network involved.
    /// </summary>
    public sealed class LanguagePreference
    {
        public const string SettingsKey = "ui.language";

        private readonly ISettingsStore _store;

        public LanguagePreference(ISettingsStore store)
        {
            _store = store;
        }

        /// <summary>
        /// Returns the saved language. On first run (nothing saved, or a saved value that is no
        /// longer supported) falls back to the device language when supported, else English.
        /// </summary>
        public AppLanguage Load(string? deviceLanguageTag)
        {
            if (LanguageCatalog.TryGetByCode(_store.GetString(SettingsKey), out var saved))
            {
                return saved;
            }

            return LanguageCatalog.TryResolveTag(deviceLanguageTag, out var device) ? device : LanguageCatalog.Default;
        }

        public void Save(AppLanguage language)
        {
            _store.SetString(SettingsKey, language.Code);
        }
    }
}
