using System.Text.RegularExpressions;

namespace Xmana.Core.Logging
{
    /// <summary>
    /// Removes anything that looks like a URL from a log message (PROMPT.md section 62/63: never log
    /// signed media URLs or signed image URLs).
    /// </summary>
    public static class LogRedactor
    {
        public const string Placeholder = "[url]";

        private static readonly Regex UrlPattern = new Regex(
            "[A-Za-z][A-Za-z0-9+.\\-]*://[^\\s\"'<>]+",
            RegexOptions.CultureInvariant);

        public static string Redact(string? message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            return UrlPattern.Replace(message!, Placeholder);
        }
    }
}
