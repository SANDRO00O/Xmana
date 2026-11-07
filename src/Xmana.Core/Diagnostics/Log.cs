using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Xmana.Core.Diagnostics
{
    /// <summary>
    /// Minimal structured logger. Every message passes through <see cref="Redact"/> so signed
    /// media/image URLs and tokens can never reach the log (PROMPT.md §63/§64).
    /// </summary>
    public static class Log
    {
        private static readonly Regex UrlPattern =
            new Regex(@"https?://[^\s""'<>]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BearerPattern =
            new Regex(@"Bearer\s+[A-Za-z0-9\-\._~\+/]+=*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static bool VerboseEnabled { get; set; } =
#if DEBUG
            true;
#else
            false;
#endif

        public static string Redact(string? message)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            var text = UrlPattern.Replace(message!, "[url]");
            text = BearerPattern.Replace(text, "[token]");
            return text;
        }

        public static void Info(string area, string message)
        {
            if (!VerboseEnabled) return;
            Write("INFO", area, message);
        }

        public static void Warn(string area, string message) => Write("WARN", area, message);

        public static void Error(string area, string message, Exception? ex = null)
        {
            var suffix = ex == null ? string.Empty : " (" + ex.GetType().Name + ")";
            Write("ERROR", area, message + suffix);
        }

        private static void Write(string level, string area, string message)
        {
            Debug.WriteLine("[" + level + "] " + area + ": " + Redact(message));
        }
    }
}
