using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Xmana.Core.Images
{
    /// <summary>
    /// On-disk image bytes. Files are named by a hash of the image URL, so no URL (signed or not) is ever written to
    /// disk. Writes are atomic, the directory is pruned oldest-first to a size budget, and files that are not valid
    /// images are deleted on read.
    /// </summary>
    public sealed class ImageDiskCache
    {
        private readonly string _directory;
        private readonly long _maxBytes;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private int _writesSincePrune;

        public ImageDiskCache(string directory, long maxBytes = 96L * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A cache directory is required.", nameof(directory));
            }

            _directory = directory;
            _maxBytes = maxBytes;
        }

        public static string KeyFor(string url)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url ?? string.Empty));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        public Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
            Task.Run<byte[]?>(() =>
            {
                var path = PathFor(key);
                try
                {
                    if (!File.Exists(path))
                    {
                        return null;
                    }

                    var bytes = File.ReadAllBytes(path);
                    if (!ImageSniffer.IsLikelyImage(bytes))
                    {
                        TryDelete(path);
                        return null;
                    }

                    return bytes;
                }
                catch (IOException)
                {
                    return null;
                }
                catch (UnauthorizedAccessException)
                {
                    return null;
                }
            }, cancellationToken);

        public async Task SetAsync(string key, byte[] bytes, CancellationToken cancellationToken)
        {
            if (!ImageSniffer.IsLikelyImage(bytes))
            {
                return;
            }

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        Directory.CreateDirectory(_directory);
                        var path = PathFor(key);
                        var temp = path + ".tmp";
                        File.WriteAllBytes(temp, bytes);
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }

                        File.Move(temp, path);
                        if (++_writesSincePrune >= 25)
                        {
                            _writesSincePrune = 0;
                            Prune();
                        }
                    }
                    catch (IOException)
                    {
                        // a failing disk must never break image display
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public Task RemoveAsync(string key) => Task.Run(() => TryDelete(PathFor(key)));

        public Task ClearAsync() => Task.Run(() =>
        {
            if (Directory.Exists(_directory))
            {
                foreach (var file in Directory.GetFiles(_directory))
                {
                    TryDelete(file);
                }
            }
        });

        private void Prune()
        {
            try
            {
                var files = new DirectoryInfo(_directory).GetFiles("*.img");
                var total = files.Sum(f => f.Length);
                if (total <= _maxBytes)
                {
                    return;
                }

                var target = (long)(_maxBytes * 0.8);
                foreach (var file in files.OrderBy(f => f.LastWriteTimeUtc))
                {
                    if (total <= target)
                    {
                        break;
                    }

                    total -= file.Length;
                    TryDelete(file.FullName);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>Deletes images downloaded more than <paramref name="maxAge"/> ago (startup housekeeping). Returns how many were removed.</summary>
        public Task<int> PurgeOlderThanAsync(TimeSpan maxAge, DateTime nowUtc)
        {
            return Task.Run(() =>
            {
                var removed = 0;
                try
                {
                    if (!Directory.Exists(_directory))
                    {
                        return 0;
                    }

                    foreach (var file in new DirectoryInfo(_directory).GetFiles("*.img"))
                    {
                        if (nowUtc - file.LastWriteTimeUtc > maxAge)
                        {
                            TryDelete(file.FullName);
                            removed++;
                        }
                    }
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }

                return removed;
            });
        }

        private string PathFor(string key) => Path.Combine(_directory, key + ".img");

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
