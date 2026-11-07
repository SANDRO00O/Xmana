using System;
using System.Globalization;
using Xmana.Core.Localization;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>Local player preferences (quality, subtitle, autoplay). Stored on the device only.</summary>
    public sealed class PlayerPreferences
    {
        public const string QualityKey = "player.quality";
        public const string SubtitleKey = "player.subtitle";
        public const string AutoplayKey = "player.autoplayNext";
        public const string ResumeKey = "player.resume";
        public const string SkipIntroKey = "player.skipIntro";

        private readonly ISettingsStore _store;

        public PlayerPreferences(ISettingsStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Preferred quality height (e.g. 720), or null when the viewer never chose.</summary>
        public int? PreferredQualityHeight
        {
            get
            {
                int value;
                return int.TryParse(_store.GetString(QualityKey), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value > 0 ? value : (int?)null;
            }
            set
            {
                if (value.HasValue && value.Value > 0)
                {
                    _store.SetString(QualityKey, value.Value.ToString(CultureInfo.InvariantCulture));
                }
            }
        }

        /// <summary>The remembered subtitle choice; <see cref="SubtitlePreference.Off"/> when subtitles were switched off.</summary>
        public SubtitlePreference Subtitle
        {
            get
            {
                switch (_store.GetString(SubtitleKey))
                {
                    case "ar":
                        return SubtitlePreference.Arabic;
                    case "en":
                        return SubtitlePreference.English;
                    case "sp":
                        return SubtitlePreference.Spanish;
                    case "off":
                        return SubtitlePreference.Off;
                    default:
                        return SubtitlePreference.Unset;
                }
            }
            set
            {
                switch (value)
                {
                    case SubtitlePreference.Arabic:
                        _store.SetString(SubtitleKey, "ar");
                        break;
                    case SubtitlePreference.English:
                        _store.SetString(SubtitleKey, "en");
                        break;
                    case SubtitlePreference.Spanish:
                        _store.SetString(SubtitleKey, "sp");
                        break;
                    case SubtitlePreference.Off:
                        _store.SetString(SubtitleKey, "off");
                        break;
                }
            }
        }

        /// <summary>Whether the next episode starts automatically after a countdown. Defaults to on; always cancellable.</summary>
        public bool AutoplayNext
        {
            get => !string.Equals(_store.GetString(AutoplayKey), "false", StringComparison.OrdinalIgnoreCase);
            set => _store.SetString(AutoplayKey, value ? "true" : "false");
        }

        /// <summary>Offer to continue from the saved position (default on). When off, playback always starts at the beginning.</summary>
        public bool ResumeEnabled
        {
            get => !string.Equals(_store.GetString(ResumeKey), "false", StringComparison.OrdinalIgnoreCase);
            set => _store.SetString(ResumeKey, value ? "true" : "false");
        }

        /// <summary>Show Skip Intro when verified timing data exists (default on). It stays hidden regardless while the skip unit is unverified.</summary>
        public bool SkipIntroEnabled
        {
            get => !string.Equals(_store.GetString(SkipIntroKey), "false", StringComparison.OrdinalIgnoreCase);
            set => _store.SetString(SkipIntroKey, value ? "true" : "false");
        }

        /// <summary>Back to factory defaults for every player preference (Reset Local Data).</summary>
        public void Reset()
        {
            _store.SetString(QualityKey, string.Empty);
            _store.SetString(SubtitleKey, string.Empty);
            _store.SetString(AutoplayKey, string.Empty);
            _store.SetString(ResumeKey, string.Empty);
            _store.SetString(SkipIntroKey, string.Empty);
        }

        /// <summary>Forgets the stored quality choice (back to automatic).</summary>
        public void ClearQuality() => _store.SetString(QualityKey, string.Empty);

        public static SubtitlePreference From(SubtitleLanguage? language)
        {
            if (language == null)
            {
                return SubtitlePreference.Off;
            }

            switch (language.Value)
            {
                case SubtitleLanguage.Arabic:
                    return SubtitlePreference.Arabic;
                case SubtitleLanguage.English:
                    return SubtitlePreference.English;
                default:
                    return SubtitlePreference.Spanish;
            }
        }

        public static SubtitleLanguage? ToLanguage(SubtitlePreference preference)
        {
            switch (preference)
            {
                case SubtitlePreference.Arabic:
                    return SubtitleLanguage.Arabic;
                case SubtitlePreference.English:
                    return SubtitleLanguage.English;
                case SubtitlePreference.Spanish:
                    return SubtitleLanguage.Spanish;
                default:
                    return null;
            }
        }
    }

    public enum SubtitlePreference
    {
        /// <summary>The viewer never chose.</summary>
        Unset,
        Off,
        Arabic,
        English,
        Spanish,
    }
}
