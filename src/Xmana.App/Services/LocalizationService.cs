using System;
using System.Linq;
using Windows.ApplicationModel.Resources.Core;
using Windows.Globalization;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Xmana.Core.Localization;

namespace Xmana.App.Services
{
    /// <summary>
    /// Centralised localisation. All user-visible text resolves through here from
    /// Strings/&lt;lang&gt;/Resources.resw; no screen contains literal UI text.
    /// Language changes apply at runtime (no restart): consumers listen to
    /// <see cref="LanguageChanged"/>, re-read strings and re-apply <see cref="FlowDirection"/>.
    /// </summary>
    internal sealed class LocalizationService
    {
        private readonly LanguagePreference _preference;
        private readonly ResourceMap _strings;
        private readonly ResourceContext _context;

        public LocalizationService(Xmana.Core.Localization.ISettingsStore store)
        {
            _preference = new LanguagePreference(store);
            _strings = ResourceManager.Current.MainResourceMap.GetSubtree("Resources");
            _context = ResourceContext.GetForViewIndependentUse();

            var deviceLanguage = GlobalizationPreferences.Languages.FirstOrDefault();
            CurrentLanguage = _preference.Load(deviceLanguage);
            Apply(CurrentLanguage);
        }

        public event EventHandler LanguageChanged;

        public AppLanguage CurrentLanguage { get; private set; }

        public FlowDirection FlowDirection =>
            CurrentLanguage.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        /// <summary>Resolves a resource key in the current language (falls back to the default language).</summary>
        public string Get(string key)
        {
            try
            {
                var candidate = _strings.GetValue(key, _context);
                var value = candidate?.ValueAsString;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }
            catch (Exception)
            {
                // fall through to the diagnostic fallback below
            }

            System.Diagnostics.Debug.WriteLine("Missing string resource: " + key);
            return key;
        }

        public void SetLanguage(AppLanguage language)
        {
            if (language == null || ReferenceEquals(language, CurrentLanguage))
            {
                return;
            }

            CurrentLanguage = language;
            _preference.Save(language);
            Apply(language);
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Apply(AppLanguage language)
        {
            ApplicationLanguages.PrimaryLanguageOverride = language.ResourceTag;
            _context.Languages = new[] { language.ResourceTag };
        }
    }
}
