using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;

namespace Xmana.Core.Caching
{
    /// <summary>
    /// Persistent cache: one small JSON file per key inside a directory the host app owns (LocalCacheFolder).
    /// Files are written atomically; anything unreadable or inconsistent is deleted and treated as a miss
    /// (corruption recovery). The directory is pruned to a size budget, oldest first.
    /// </summary>
    public sealed class FileResponseCache : IResponseCache
    {
        private const int FormatVersion = 1;
        private const string Extension = ".json";

        private readonly string _directory;
        private readonly long _maxBytes;
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);
        private int _writesSincePrune;

        public FileResponseCache(string directory, long maxBytes = 32L * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("A cache directory is required.", nameof(directory));
            }

            _directory = directory;
            _maxBytes = maxBytes;
        }

        public Task<CacheEntry?> GetAsync(string key, CancellationToken cancellationToken)
        {
            return Task.Run<CacheEntry?>(() => ReadEntry(key), cancellationToken);
        }

        public async Task SetAsync(CacheEntry entry, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => WriteEntry(entry), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => TryDelete(PathFor(key)), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() =>
                {
                    if (!Directory.Exists(_directory))
                    {
                        return;
                    }

                    foreach (var file in Directory.GetFiles(_directory))
                    {
                        TryDelete(file);
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private CacheEntry? ReadEntry(string key)
        {
            var path = PathFor(key);
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var envelope = JsonParser.Parse(File.ReadAllText(path, Encoding.UTF8)) as JObject;
                var version = JsonRead.Int(JsonRead.Get(envelope, "v"));
                var storedKey = JsonRead.String(JsonRead.Get(envelope, "key"));
                var payload = JsonRead.String(JsonRead.Get(envelope, "payload"));
                var stored = JsonRead.String(JsonRead.Get(envelope, "storedUtc"));

                DateTime storedUtc;
                if (version != FormatVersion
                    || storedKey != key
                    || payload == null
                    || stored == null
                    || !DateTime.TryParseExact(stored, "o", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out storedUtc))
                {
                    TryDelete(path);
                    return null;
                }

                return new CacheEntry(key, payload, storedUtc.ToUniversalTime());
            }
            catch (JsonException)
            {
                TryDelete(path);
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private void WriteEntry(CacheEntry entry)
        {
            Directory.CreateDirectory(_directory);

            var envelope = new JObject
            {
                { "v", FormatVersion },
                { "key", entry.Key },
                { "storedUtc", entry.StoredUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture) },
                { "payload", entry.Json },
            };

            var path = PathFor(entry.Key);
            var temp = path + ".tmp";
            File.WriteAllText(temp, envelope.ToString(Formatting.None), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(temp, path);

            if (++_writesSincePrune >= 20)
            {
                _writesSincePrune = 0;
                Prune();
            }
        }

        private void Prune()
        {
            try
            {
                var files = new DirectoryInfo(_directory).GetFiles("*" + Extension);
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
                // pruning is best effort
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Deletes entries written more than <paramref name="maxAge"/> ago (startup housekeeping: nothing here is ever trusted past its
        /// error-fallback window anyway). Returns how many files were removed.
        /// </summary>
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

                    foreach (var file in new DirectoryInfo(_directory).GetFiles("*" + Extension))
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

        private string PathFor(string key)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
                }

                return Path.Combine(_directory, builder + Extension);
            }
        }

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
