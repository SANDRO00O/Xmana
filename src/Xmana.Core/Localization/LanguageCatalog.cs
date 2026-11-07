using System;
using System.Collections.Generic;

namespace Xmana.Core.Localization
{
    /// <summary>
    /// The set of supported UI languages. Adding a language means adding one entry here plus
    /// one resource folder; no screen needs to change.
    /// </summary>
    public static class LanguageCatalog
    {
        public static readonly AppLanguage English = new AppLanguage("en", "en-US", "English", false);

        // "العربية"
        public static readonly AppLanguage Arabic = new AppLanguage("ar", "ar", "\u0627\u0644\u0639\u0631\u0628\u064A\u0629", true);

        public static IReadOnlyList<AppLanguage> All { get; } = new[] { English, Arabic };

        public static AppLanguage Default => English;

        /// <summary>Finds a language by its persisted code. Case-insensitive.</summary>
        public static bool TryGetByCode(string? code, out AppLanguage language)
        {
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }

            language = Default;
            return false;
        }

        /// <summary>
        /// Maps any BCP-47 tag ("ar-SA", "en-GB", "ar") to a supported language by its primary
        /// subtag. Returns false (and <see cref="Default"/>) when the tag is empty or unsupported.
        /// </summary>
        public static bool TryResolveTag(string? tag, out AppLanguage language)
        {
            language = Default;
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            var trimmed = tag!.Trim();
            var separator = trimmed.IndexOfAny(new[] { '-', '_' });
            var primary = separator >= 0 ? trimmed.Substring(0, separator) : trimmed;
            return TryGetByCode(primary, out language);
        }
    }
}
