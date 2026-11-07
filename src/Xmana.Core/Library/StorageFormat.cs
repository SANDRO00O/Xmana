using System.Globalization;

namespace Xmana.Core.Library
{
    public static class StorageFormat
    {
        /// <summary>"0 B", "512 B", "1.5 KB", "12.4 MB", "1.2 GB" (invariant digits, one decimal above bytes).</summary>
        public static string Bytes(long bytes)
        {
            if (bytes <= 0)
            {
                return "0 B";
            }

            if (bytes < 1024)
            {
                return bytes.ToString(CultureInfo.InvariantCulture) + " B";
            }

            double value = bytes;
            string[] units = { "KB", "MB", "GB", "TB" };
            var unit = -1;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }
    }
}
