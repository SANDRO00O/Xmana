using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Json;

namespace Xmana.Core.Playback
{
    /// <summary>Local progress storage (PROMPT.md sections 33, 43). Failures are never fatal: a store that cannot read or write behaves as empty.</summary>
    public interface IProgressStore
    {
        PlaybackProgress? Get(string videoId);

        void Save(PlaybackProgress progress);

        void Remove(string videoId);

        /// <summary>Most recently played first.</summary>
        IReadOnlyList<PlaybackProgress> Recent(int count);

        /// <summary>Forgets everything (Clear History / Reset Local Data).</summary>
        void Clear();
    }

    public sealed class MemoryProgressStore : IProgressStore
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, PlaybackProgress> _items = new Dictionary<string, PlaybackProgress>(StringComparer.Ordinal);

        public PlaybackProgress? Get(string videoId)
        {
            lock (_gate)
            {
                PlaybackProgress value;
                return _items.TryGetValue(videoId, out value) ? value : null;
            }
        }

        public void Save(PlaybackProgress progress)
        {
            lock (_gate)
            {
                _items[progress.VideoId] = progress;
            }
        }

        public void Remove(string videoId)
        {
            lock (_gate)
            {
                _items.Remove(videoId);
            }
        }

        public IReadOnlyList<PlaybackProgress> Recent(int count)
        {
            lock (_gate)
            {
                return _items.Values.OrderByDescending(p => p.LastPlayedUtc).Take(Math.Max(0, count)).ToList();
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _items.Clear();
            }
        }
    }

    /// <summary>
    /// One small JSON file with the most recent entries (bounded). Written atomically; a corrupt or missing file is treated as empty
    /// and replaced on the next save. Contains only ids, positions and timestamps: never a stream URL.
    /// </summary>
    public sealed class FileProgressStore : IProgressStore
    {
        private const int FormatVersion = 1;
        private const int MaxEntries = 500;

        private readonly string _path;
        private readonly object _gate = new object();
        private Dictionary<string, PlaybackProgress>? _items;

        public FileProgressStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A file path is required.", nameof(path));
            }

            _path = path;
        }

        public PlaybackProgress? Get(string videoId)
        {
            lock (_gate)
            {
                PlaybackProgress value;
                return Load().TryGetValue(videoId, out value) ? value : null;
            }
        }

        public void Save(PlaybackProgress progress)
        {
            lock (_gate)
            {
                var items = Load();
                items[progress.VideoId] = progress;
                if (items.Count > MaxEntries)
                {
                    foreach (var old in items.Values.OrderBy(p => p.LastPlayedUtc).Take(items.Count - MaxEntries).ToList())
                    {
                        items.Remove(old.VideoId);
                    }
                }

                Persist(items);
            }
        }

        public void Remove(string videoId)
        {
            lock (_gate)
            {
                var items = Load();
                if (items.Remove(videoId))
                {
                    Persist(items);
                }
            }
        }

        public IReadOnlyList<PlaybackProgress> Recent(int count)
        {
            lock (_gate)
            {
                return Load().Values.OrderByDescending(p => p.LastPlayedUtc).Take(Math.Max(0, count)).ToList();
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _items = new Dictionary<string, PlaybackProgress>(StringComparer.Ordinal);
                try
                {
                    if (File.Exists(_path))
                    {
                        File.Delete(_path);
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

        private Dictionary<string, PlaybackProgress> Load()
        {
            if (_items != null)
            {
                return _items;
            }

            var items = new Dictionary<string, PlaybackProgress>(StringComparer.Ordinal);
            try
            {
                if (File.Exists(_path))
                {
                    var root = JsonParser.Parse(File.ReadAllText(_path, Encoding.UTF8)) as JObject;
                    var list = JsonRead.Array(JsonRead.Get(root, "items"));
                    if (JsonRead.Int(JsonRead.Get(root, "v")) == FormatVersion && list != null)
                    {
                        foreach (var token in list)
                        {
                            var entry = Read(token);
                            if (entry != null)
                            {
                                items[entry.VideoId] = entry;
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // corrupt file: start empty, the next save replaces it
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            _items = items;
            return items;
        }

        private static PlaybackProgress? Read(JToken token)
        {
            var id = JsonRead.Text(JsonRead.Get(token, "id"));
            var position = JsonRead.Double(JsonRead.Get(token, "pos"));
            var duration = JsonRead.Double(JsonRead.Get(token, "dur"));
            var played = JsonRead.String(JsonRead.Get(token, "played"));
            DateTime playedUtc;
            if (id == null || position == null || position < 0 || duration == null || duration < 0 || played == null
                || !DateTime.TryParseExact(played, "o", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out playedUtc))
            {
                return null;
            }

            return new PlaybackProgress(
                id,
                JsonRead.Text(JsonRead.Get(token, "series")),
                JsonRead.Int(JsonRead.Get(token, "season")),
                JsonRead.Int(JsonRead.Get(token, "episode")),
                TimeSpan.FromSeconds(position.Value),
                TimeSpan.FromSeconds(duration.Value),
                playedUtc.ToUniversalTime(),
                JsonRead.Bool(JsonRead.Get(token, "done")) == true);
        }

        private void Persist(Dictionary<string, PlaybackProgress> items)
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var list = new JArray(items.Values.OrderByDescending(p => p.LastPlayedUtc).Select(p => new JObject
                {
                    { "id", p.VideoId },
                    { "series", p.SeriesId },
                    { "season", p.Season },
                    { "episode", p.Episode },
                    { "pos", p.Position.TotalSeconds },
                    { "dur", p.Duration.TotalSeconds },
                    { "played", p.LastPlayedUtc.ToUniversalTime().ToString("o", System.Globalization.CultureInfo.InvariantCulture) },
                    { "done", p.Completed },
                }));
                var root = new JObject { { "v", FormatVersion }, { "items", list } };

                var temp = _path + ".tmp";
                File.WriteAllText(temp, root.ToString(Formatting.None), new UTF8Encoding(false));
                if (File.Exists(_path))
                {
                    File.Delete(_path);
                }

                File.Move(temp, _path);
            }
            catch (IOException)
            {
                // a failing disk must never interrupt playback
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
