using System;
using System.Globalization;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>Small presentation helpers for the player.</summary>
    public static class PlaybackFormat
    {
        /// <summary>"7:05" or "1:02:03". Negative and absurd values are clamped to zero.</summary>
        public static string Time(TimeSpan value)
        {
            if (value < TimeSpan.Zero || value > TimeSpan.FromDays(2))
            {
                value = TimeSpan.Zero;
            }

            var total = (long)value.TotalSeconds;
            var hours = total / 3600;
            var minutes = total % 3600 / 60;
            var seconds = total % 60;
            return hours > 0
                ? string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, seconds)
                : string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, seconds);
        }

        /// <summary>Progress bar value in 0..1; unknown durations give 0.</summary>
        public static double Fraction(TimeSpan position, TimeSpan duration)
        {
            if (duration <= TimeSpan.Zero)
            {
                return 0;
            }

            var fraction = position.TotalSeconds / duration.TotalSeconds;
            return fraction < 0 ? 0 : fraction > 1 ? 1 : fraction;
        }

        /// <summary>
        /// A subtitle language in its own script (autonym convention, like the interface-language picker), so it is
        /// deliberately not translated.
        /// </summary>
        public static string SubtitleLanguageName(SubtitleLanguage language)
        {
            switch (language)
            {
                case SubtitleLanguage.Arabic:
                    return "\u0627\u0644\u0639\u0631\u0628\u064A\u0629"; // العربية
                case SubtitleLanguage.English:
                    return "English";
                default:
                    return "Espa\u00F1ol";
            }
        }
    }
}
