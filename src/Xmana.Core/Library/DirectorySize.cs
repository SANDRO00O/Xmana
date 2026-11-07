using System;
using System.IO;

namespace Xmana.Core.Library
{
    public static class DirectorySize
    {
        /// <summary>Total size in bytes of every file under a directory; 0 when it does not exist or cannot be read.</summary>
        public static long Of(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return 0;
            }

            try
            {
                if (!Directory.Exists(path))
                {
                    return 0;
                }

                long total = 0;
                foreach (var file in new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    total += file.Length;
                }

                return total;
            }
            catch (IOException)
            {
                return 0;
            }
            catch (UnauthorizedAccessException)
            {
                return 0;
            }
        }
    }
}
