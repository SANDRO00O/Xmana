using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xmana.Core.Caching;
using Xmana.Core.Json;
using Xmana.Core.Models;

namespace Xmana.Core.Library
{
    /// <summary>
    /// Favorites, Watchlist, title snapshots and search history (PROMPT.md section 33): local only, no account, no sync, never uploaded.
    /// Persisted as one small JSON file with bounded lists; a corrupt or missing file is treated as empty. Mutations are saved
    /// immediately; <see cref="Changed"/> is raised outside the lock.
    /// </summary>
    public sealed class LocalLibrary
    {
        public const int MaxListSize = 500;
        public const int MaxSnapshots = 800;
        public const int MaxSearches = 20;
        public const int MaxSearchLength = 80;

        private const int FormatVersion = 1;
        private static readonly Regex Spaces = new Regex("\\s+", RegexOptions.CultureInvariant);

        private readonly ILibraryStorage _storage;
        private readonly IClock _clock;
        private readonly object _gate = new object();
        private bool _loaded;
        private List<Entry> _favorites = new List<Entry>();
        private List<Entry> _watchlist = new List<Entry>();
        private Dictionary<string, LibraryItem> _snapshots = new Dictionary<string, LibraryItem>(StringComparer.Ordinal);
        private List<string> _searches = new List<string>();

        public LocalLibrary(ILibraryStorage storage, IClock? clock = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _clock = clock ?? SystemClock.Instance;
        }

        public event EventHandler? Changed;

        // ---- favorites and watchlist ------------------------------------------------------------

        public bool IsFavorite(string videoId) => Read(() => _favorites.Any(e => e.Id == videoId));

        public bool IsWatchlisted(string videoId) => Read(() => _watchlist.Any(e => e.Id == videoId));

        /// <summary>Adds or removes a favorite. Returns the new state (true = now a favorite).</summary>
        public bool ToggleFavorite(VideoSummary video) => Toggle(video, false);

        public bool ToggleWatchlist(VideoSummary video) => Toggle(video, true);

        public IReadOnlyList<VideoSummary> Favorites() => Read(() => Materialise(_favorites));

        public IReadOnlyList<VideoSummary> Watchlist() => Read(() => Materialise(_watchlist));

        private bool Toggle(VideoSummary video, bool watchlist)
        {
            if (video == null)
            {
                throw new ArgumentNullException(nameof(video));
            }

            bool nowOn;
            lock (_gate)
            {
                EnsureLoaded();
                var list = watchlist ? _watchlist : _favorites;
                var existing = list.FindIndex(e => e.Id == video.Id);
                if (existing >= 0)
                {
                    list.RemoveAt(existing);
                    nowOn = false;
                }
                else
                {
                    _snapshots[video.Id] = LibraryItem.From(video, _clock.UtcNow);
                    list.Insert(0, new Entry(video.Id, _clock.UtcNow));
                    if (list.Count > MaxListSize)
                    {
                        list.RemoveRange(MaxListSize, list.Count - MaxListSize);
                    }

                    PruneSnapshots();
                    nowOn = true;
                }

                Save();
            }

            RaiseChanged();
            return nowOn;
        }

        // ---- snapshots --------------------------------------------------------------------------

        /// <summary>Remembers how a title looks, so History and Continue Watching can show it offline. Cheap when nothing changed.</summary>
        public void RememberSnapshot(VideoSummary video)
        {
            if (video == null)
            {
                return;
            }

            var changed = false;
            lock (_gate)
            {
                EnsureLoaded();
                var fresh = LibraryItem.From(video, _clock.UtcNow);
                LibraryItem current;
                if (_snapshots.TryGetValue(video.Id, out current) && current.SameContent(fresh))
                {
                    _snapshots[video.Id] = fresh; // only the "last seen" time moves; no file write needed
                    return;
                }

                _snapshots[video.Id] = fresh;
                PruneSnapshots();
                Save();
                changed = true;
            }

            if (changed)
            {
                RaiseChanged();
            }
        }

        public VideoSummary? GetSnapshot(string videoId)
        {
            lock (_gate)
            {
                EnsureLoaded();
                LibraryItem item;
                return _snapshots.TryGetValue(videoId, out item) ? item.ToSummary() : null;
            }
        }

        // ---- search history ---------------------------------------------------------------------

        public IReadOnlyList<string> RecentSearches(int max = 8) => Read(() => (IReadOnlyList<string>)_searches.Take(Math.Max(0, max)).ToList());

        /// <summary>Records a search term (trimmed, spaces collapsed, newest first, case-insensitive duplicates merged).</summary>
        public void AddSearch(string? text)
        {
            var normalised = Normalise(text);
            if (normalised == null)
            {
                return;
            }

            lock (_gate)
            {
                EnsureLoaded();
                _searches.RemoveAll(s => string.Equals(s, normalised, StringComparison.OrdinalIgnoreCase));
                _searches.Insert(0, normalised);
                if (_searches.Count > MaxSearches)
                {
                    _searches.RemoveRange(MaxSearches, _searches.Count - MaxSearches);
                }

                Save();
            }

            RaiseChanged();
        }

        public void RemoveSearch(string text)
        {
            lock (_gate)
            {
                EnsureLoaded();
                if (_searches.RemoveAll(s => string.Equals(s, text, StringComparison.OrdinalIgnoreCase)) == 0)
                {
                    return;
                }

                Save();
            }

            RaiseChanged();
        }

        public void ClearSearches()
        {
            lock (_gate)
            {
                EnsureLoaded();
                if (_searches.Count == 0)
                {
                    return;
                }

                _searches.Clear();
                Save();
            }

            RaiseChanged();
        }

        /// <summary>Removes everything this class stores (favorites, watchlist, snapshots, searches) and its file.</summary>
        public void ClearAll()
        {
            lock (_gate)
            {
                _favorites = new List<Entry>();
                _watchlist = new List<Entry>();
                _snapshots = new Dictionary<string, LibraryItem>(StringComparer.Ordinal);
                _searches = new List<string>();
                _loaded = true;
                _storage.Delete();
            }

            RaiseChanged();
        }

        // ---- persistence ------------------------------------------------------------------------

        private void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                var text = _storage.Read();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                var root = JsonParser.Parse(text) as JObject;
                if (JsonRead.Int(JsonRead.Get(root, "v")) != FormatVersion)
                {
                    return;
                }

                foreach (var item in JsonRead.Array(JsonRead.Get(root, "items")) ?? new JArray())
                {
                    var read = ReadItem(item);
                    if (read != null)
                    {
                        _snapshots[read.Id] = read;
                    }
                }

                _favorites = ReadEntries(JsonRead.Get(root, "favorites"));
                _watchlist = ReadEntries(JsonRead.Get(root, "watchlist"));

                // A list entry without a snapshot cannot be shown, so it is dropped on load.
                _favorites.RemoveAll(e => !_snapshots.ContainsKey(e.Id));
                _watchlist.RemoveAll(e => !_snapshots.ContainsKey(e.Id));

                foreach (var term in JsonRead.Array(JsonRead.Get(root, "searches")) ?? new JArray())
                {
                    var normalised = Normalise(JsonRead.String(term));
                    if (normalised != null && !_searches.Any(s => string.Equals(s, normalised, StringComparison.OrdinalIgnoreCase)))
                    {
                        _searches.Add(normalised);
                    }
                }

                if (_searches.Count > MaxSearches)
                {
                    _searches.RemoveRange(MaxSearches, _searches.Count - MaxSearches);
                }
            }
            catch (JsonException)
            {
                // corrupt file: start empty; the next save replaces it
                _favorites = new List<Entry>();
                _watchlist = new List<Entry>();
                _snapshots = new Dictionary<string, LibraryItem>(StringComparer.Ordinal);
                _searches = new List<string>();
            }
        }

        private void Save()
        {
            var root = new JObject
            {
                { "v", FormatVersion },
                { "favorites", new JArray(_favorites.Select(WriteEntry)) },
                { "watchlist", new JArray(_watchlist.Select(WriteEntry)) },
                { "items", new JArray(_snapshots.Values.Select(WriteItem)) },
                { "searches", new JArray(_searches) },
            };
            _storage.Write(root.ToString(Formatting.None));
        }

        private static JObject WriteEntry(Entry entry) => new JObject
        {
            { "id", entry.Id },
            { "added", entry.AddedUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) },
        };

        private static JObject WriteItem(LibraryItem item)
        {
            var json = new JObject { { "id", item.Id }, { "seen", item.SeenUtc.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) } };
            Put(json, "en", item.TitleEn);
            Put(json, "ar", item.TitleAr);
            Put(json, "year", item.Year);
            Put(json, "kind", item.Kind);
            Put(json, "img", item.Image);
            Put(json, "thumb", item.ImageThumb);
            Put(json, "medium", item.ImageMediumThumb);
            Put(json, "root", item.RootSeries);
            if (item.Stars.HasValue)
            {
                json["stars"] = item.Stars.Value;
            }

            if (item.Season.HasValue)
            {
                json["season"] = item.Season.Value;
            }

            if (item.Episode.HasValue)
            {
                json["episode"] = item.Episode.Value;
            }

            return json;
        }

        private static void Put(JObject json, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                json[name] = value;
            }
        }

        private static LibraryItem? ReadItem(JToken token)
        {
            var id = JsonRead.Text(JsonRead.Get(token, "id"));
            if (id == null)
            {
                return null;
            }

            return new LibraryItem(
                id,
                JsonRead.Text(JsonRead.Get(token, "en")),
                JsonRead.Text(JsonRead.Get(token, "ar")),
                JsonRead.Text(JsonRead.Get(token, "year")),
                JsonRead.Text(JsonRead.Get(token, "kind")),
                JsonRead.Double(JsonRead.Get(token, "stars")),
                JsonRead.Text(JsonRead.Get(token, "img")),
                JsonRead.Text(JsonRead.Get(token, "thumb")),
                JsonRead.Text(JsonRead.Get(token, "medium")),
                JsonRead.Text(JsonRead.Get(token, "root")),
                JsonRead.Int(JsonRead.Get(token, "season")),
                JsonRead.Int(JsonRead.Get(token, "episode")),
                ParseTime(JsonRead.String(JsonRead.Get(token, "seen"))));
        }

        private static List<Entry> ReadEntries(JToken? token)
        {
            var list = new List<Entry>();
            foreach (var item in JsonRead.Array(token) ?? new JArray())
            {
                var id = JsonRead.Text(JsonRead.Get(item, "id"));
                if (id != null && !list.Any(e => e.Id == id))
                {
                    list.Add(new Entry(id, ParseTime(JsonRead.String(JsonRead.Get(item, "added")))));
                }
            }

            return list.OrderByDescending(e => e.AddedUtc).Take(MaxListSize).ToList();
        }

        private static DateTime ParseTime(string? text)
        {
            DateTime value;
            return text != null && DateTime.TryParseExact(text, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out value)
                ? value.ToUniversalTime()
                : DateTime.MinValue;
        }

        // ---- helpers ----------------------------------------------------------------------------

        private IReadOnlyList<VideoSummary> Materialise(List<Entry> entries)
        {
            EnsureLoaded();
            var result = new List<VideoSummary>();
            foreach (var entry in entries)
            {
                LibraryItem item;
                if (_snapshots.TryGetValue(entry.Id, out item))
                {
                    result.Add(item.ToSummary());
                }
            }

            return result;
        }

        private void PruneSnapshots()
        {
            if (_snapshots.Count <= MaxSnapshots)
            {
                return;
            }

            var keep = new HashSet<string>(_favorites.Select(e => e.Id).Concat(_watchlist.Select(e => e.Id)), StringComparer.Ordinal);
            foreach (var old in _snapshots.Values.Where(s => !keep.Contains(s.Id)).OrderBy(s => s.SeenUtc).Take(_snapshots.Count - MaxSnapshots).ToList())
            {
                _snapshots.Remove(old.Id);
            }
        }

        private static string? Normalise(string? text)
        {
            var collapsed = Spaces.Replace((text ?? string.Empty).Trim(), " ");
            return collapsed.Length == 0 || collapsed.Length > MaxSearchLength ? null : collapsed;
        }

        private T Read<T>(Func<T> getter)
        {
            lock (_gate)
            {
                EnsureLoaded();
                return getter();
            }
        }

        private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

        private sealed class Entry
        {
            public Entry(string id, DateTime addedUtc)
            {
                Id = id;
                AddedUtc = addedUtc;
            }

            public string Id { get; }

            public DateTime AddedUtc { get; }
        }
    }
}
