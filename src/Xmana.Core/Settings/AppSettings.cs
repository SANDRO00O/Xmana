using System;
using Xmana.Core.Localization;

namespace Xmana.Core.Settings
{
    /// <summary>Typed view over local-only preferences. No account or cloud sync is involved.</summary>
    public sealed class AppSettings
    {
        private const string LanguageKey = "ui.language";

        private readonly ISettingsStore _store;

        public AppSettings(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>The saved UI language, or null when the user never chose one.</summary>
        public string? SavedLanguage
        {
            get
            {
                var raw = _store.GetString(LanguageKey);
                return string.IsNullOrWhiteSpace(raw) ? null : LanguageCodes.Normalize(raw);
            }
        }

        public void SaveLanguage(string code) =>
            _store.SetString(LanguageKey, LanguageCodes.Normalize(code));
    }
}
