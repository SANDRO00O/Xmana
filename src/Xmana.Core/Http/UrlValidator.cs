using System;
using System.Linq;

namespace Xmana.Core.Http
{
    /// <summary>
    /// "Validate remote URLs" (PROMPT.md section 62). API traffic is only allowed to the documented
    /// public hosts over HTTPS, without embedded credentials.
    /// </summary>
    public static class UrlValidator
    {
        public static readonly string[] AllowedApiHosts =
        {
            "cinemana.shabakaty.cc",
            "recommend.shabakaty.cc",
            "rating.shabakaty.cc",
        };

        public static bool IsAllowedApiUri(Uri? uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }

            return AllowedApiHosts.Any(host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Parses a server-supplied URL (image, trailer ...) and accepts only absolute HTTPS URLs.</summary>
        public static bool TryGetHttpsUri(string? value, out Uri uri)
        {
            uri = null!;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            Uri? parsed;
            if (!Uri.TryCreate(value!.Trim(), UriKind.Absolute, out parsed) || parsed == null)
            {
                return false;
            }

            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) || !string.IsNullOrEmpty(parsed.UserInfo))
            {
                return false;
            }

            uri = parsed;
            return true;
        }
    }
}
