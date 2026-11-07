using System;
using System.Text;

namespace Xmana.Core.Api
{
    /// <summary>Simple title search keyword encoding: Base64URL without padding (API.md section 5).</summary>
    public static class SearchEncoding
    {
        /// <summary>"Conan" -> "Q29uYW4". UTF-8 bytes, '+' -> '-', '/' -> '_', no '=' padding.</summary>
        public static string EncodeKeyword(string keyword)
        {
            if (keyword == null)
            {
                throw new ArgumentNullException(nameof(keyword));
            }

            var bytes = new UTF8Encoding(false).GetBytes(keyword);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>Inverse of <see cref="EncodeKeyword"/> (used by tests and diagnostics).</summary>
        public static string DecodeKeyword(string encoded)
        {
            if (encoded == null)
            {
                throw new ArgumentNullException(nameof(encoded));
            }

            var padded = encoded.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
            }

            return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(padded));
        }
    }
}
