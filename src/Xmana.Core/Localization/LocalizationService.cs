using System;
using System.Collections.Generic;
using System.Globalization;
using Xmana.Core.Diagnostics;

namespace Xmana.Core.Localization
{
    /// <summary>
    /// Centralized localization. All user-visible text resolves through here; translations live
    /// in separate catalogs (<see cref="StringsEn"/>, <see cref="StringsAr"/>) so adding a language
    /// never touches screens (PROMPT.md section 3).
    /// </summary>
    public sealed class LocalizationService : ILocalizationService
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _catalogs;
        private string _language;

        public LocalizationService(string initialLanguage)
            : this(CreateDefaultCatalogs(), initialLanguage)
        {
        }

        public LocalizationService(
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs,
            string initialLanguage)
        {
            _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
            _language = LanguageCodes.Normalize(initialLanguage);
            Culture = CreateCulture(_language);
        }

        public event EventHandler? LanguageChanged;

        public string CurrentLanguage => _language;

        public bool IsRightToLeft => LanguageCodes.IsRightToLeft(_language);

        public CultureInfo Culture { get; private set; }

        public string Get(string key)
        {
            if (TryLookup(_language, key, out var value)) return value;
            if (TryLookup(LanguageCodes.English, key, out value)) return value;
            Log.Warn("Localization", "Missing string key: " + key);
            return key;
        }

        public string Format(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(Culture, template, args);
            }
            catch (FormatException)
            {
                Log.Warn("Localization", "Bad format string for key: " + key);
                return template;
            }
        }

        public void SetLanguage(string code)
        {
            var normalized = LanguageCodes.Normalize(code);
            if (string.Equals(normalized, _language, StringComparison.Ordinal)) return;
            _language = normalized;
            Culture = CreateCulture(normalized);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool TryLookup(string language, string key, out string value)
        {
            value = string.Empty;
            if (!_catalogs.TryGetValue(language, out var catalog)) return false;
            if (!catalog.TryGetValue(key, out var found)) return false;
            if (string.IsNullOrEmpty(found)) return false;
            value = found;
            return true;
        }

        private static CultureInfo CreateCulture(string language)
        {
            try
            {
                return new CultureInfo(language);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CreateDefaultCatalogs()
        {
            return new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                [LanguageCodes.English] = StringsEn.All,
                [LanguageCodes.Arabic] = StringsAr.All,
            };
        }
    }
}
