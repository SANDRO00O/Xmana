using System;
using System.Globalization;

namespace Xmana.Core.Localization
{
    public interface ILocalizationService
    {
        string CurrentLanguage { get; }
        bool IsRightToLeft { get; }
        CultureInfo Culture { get; }

        /// <summary>Raised after the language (and therefore direction) changed.</summary>
        event EventHandler? LanguageChanged;

        string Get(string key);
        string Format(string key, params object[] args);
        void SetLanguage(string code);
    }
}
