using System.Text.RegularExpressions;

namespace Xmana.Core.Details
{
    /// <summary>
    /// Presents the API's <c>duration</c> string. API.md documents only "Duration value" and never its unit, so a bare number
    /// is NOT converted (guessing seconds versus minutes would show wrong data). Only clock-style values ("1:35:20",
    /// "95:10") are shown, exactly as sent; anything else is treated as unavailable.
    /// </summary>
    public static class DurationFormat
    {
        private static readonly Regex Clock = new Regex("^\\d{1,3}:[0-5]\\d(?::[0-5]\\d)?$", RegexOptions.CultureInvariant);

        public static bool TryFormat(string? raw, out string text)
        {
            text = string.Empty;
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed) || !Clock.IsMatch(trimmed!))
            {
                return false;
            }

            text = trimmed!;
            return true;
        }
    }
}
