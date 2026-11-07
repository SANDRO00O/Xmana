using System.Linq;
using Windows.Globalization;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Xmana.Core.Localization;
using Xmana.Core.Settings;

namespace Xmana.App.Services
{
    /// <summary>Small composition root for services shared by every page.</summary>
    internal static class AppServices
    {
        public static ILocalizationService Localization { get; private set; } = null!;
        public static AppSettings Settings { get; private set; } = null!;

        public static void Initialize()
        {
            if (Localization != null) return;

            Settings = new AppSettings(new UwpSettingsStore());

            var language = Settings.SavedLanguage ?? DetectDeviceLanguage();
            Localization = new LocalizationService(language);
            Loc.Attach(Localization);
        }

        /// <summary>Persists and applies a new UI language. Direction/text update live, no restart.</summary>
        public static void ChangeLanguage(string code)
        {
            var normalized = LanguageCodes.Normalize(code);
            Settings.SaveLanguage(normalized);
            Localization.SetLanguage(normalized);
            if (Window.Current?.Content is FrameworkElement root)
            {
                ApplyLanguageToWindow(root);
            }
        }

        /// <summary>One layout system: only FlowDirection and Language change between Arabic and English.</summary>
        public static void ApplyLanguageToWindow(FrameworkElement root)
        {
            root.FlowDirection = Localization.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            root.Language = Localization.CurrentLanguage;
            try
            {
                ApplicationLanguages.PrimaryLanguageOverride = Localization.CurrentLanguage;
            }
            catch (System.Exception)
            {
                // Non-fatal: only affects system-provided controls.
            }
        }

        private static string DetectDeviceLanguage()
        {
            try
            {
                var first = GlobalizationPreferences.Languages.FirstOrDefault();
                return LanguageCodes.Normalize(first);
            }
            catch (System.Exception)
            {
                return LanguageCodes.English;
            }
        }
    }
}
