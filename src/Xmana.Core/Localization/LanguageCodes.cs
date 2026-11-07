using System;
using System.Collections.Generic;

namespace Xmana.Core.Localization
{
    public static class LanguageCodes
    {
        public const string English = "en";
        public const string Arabic = "ar";

        public static readonly IReadOnlyList<string> Supported = new[] { English, Arabic };

        /// <summary>Maps any BCP-47 tag (en-US, ar-SA, ...) to a supported UI language code.</summary>
        public static string Normalize(string? code)
        {
            if (!string.IsNullOrWhiteSpace(code) &&
                code!.Trim().StartsWith("ar", StringComparison.OrdinalIgnoreCase))
            {
                return Arabic;
            }
            return English;
        }

        public static bool IsRightToLeft(string code) =>
            string.Equals(code, Arabic, StringComparison.OrdinalIgnoreCase);
    }
}
