using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Storage;

namespace Xmana.Services
{
    public sealed class PlaybackProgress
    {
        public string VideoId { get; set; }
        public string SeriesId { get; set; }
        public string Season { get; set; }
        public string Episode { get; set; }
        public double PositionSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public DateTime LastPlayedUtc { get; set; }
        public bool Completed { get; set; }
    }

    public sealed class LocalStateStore
    {
        private readonly ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private const int MaxSearchHistory = 15;
        private const int MaxRecentlyViewed = 30;

        public string Language
        {
            get => settings.Values["language"] as string ?? "en";
            set => settings.Values["language"] = value == "ar" ? "ar" : "en";
        }

        public string PreferredQuality
        {
            get => settings.Values["preferredQuality"] as string ?? "Auto";
            set => settings.Values["preferredQuality"] = value ?? "Auto";
        }

        public string PreferredSubtitle
        {
            get => settings.Values["preferredSubtitle"] as string ?? "Off";
            set => settings.Values["preferredSubtitle"] = value ?? "Off";
        }

        public bool AutoNext
        {
            get => ReadBoolean("autoNext", false);
            set => settings.Values["autoNext"] = value;
        }

        public bool ResumePlayback
        {
            get => ReadBoolean("resumePlayback", true);
            set => settings.Values["resumePlayback"] = value;
        }

        public bool SkipIntro
        {
            get => ReadBoolean("skipIntro", true);
            set => settings.Values["skipIntro"] = value;
        }

        public HashSet<string> Favorites => ReadIdSet("favorites");
        public HashSet<string> Watchlist => ReadIdSet("watchlist");

        public IList<string> SearchHistory => ReadStringList("searchHistory");
        public IList<string> RecentlyViewed => ReadStringList("recentlyViewed");

        public void ToggleFavorite(string id) => ToggleId("favorites", id);
        public void ToggleWatchlist(string id) => ToggleId("watchlist", id);
        public bool IsFavorite(string id) => Favorites.Contains(id ?? String.Empty);
        public bool IsInWatchlist(string id) => Watchlist.Contains(id ?? String.Empty);

        public void AddSearch(string query)
        {
            if (String.IsNullOrWhiteSpace(query)) return;
            var values = SearchHistory.Where(x => !String.Equals(x, query, StringComparison.OrdinalIgnoreCase)).ToList();
            values.Insert(0, query.Trim());
            WriteStringList("searchHistory", values.Take(MaxSearchHistory));
        }

        public void ClearSearchHistory() => settings.Values["searchHistory"] = "[]";

        public void AddRecentlyViewed(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            var values = RecentlyViewed.Where(x => x != id).ToList();
            values.Insert(0, id);
            WriteStringList("recentlyViewed", values.Take(MaxRecentlyViewed));
        }

        public IList<PlaybackProgress> PlaybackItems
        {
            get
            {
                try
                {
                    var raw = settings.Values["playbackProgress"] as string;
                    return String.IsNullOrWhiteSpace(raw)
                        ? new List<PlaybackProgress>()
                        : JsonConvert.DeserializeObject<List<PlaybackProgress>>(raw) ?? new List<PlaybackProgress>();
                }
                catch
                {
                    settings.Values.Remove("playbackProgress");
                    return new List<PlaybackProgress>();
                }
            }
        }

        public PlaybackProgress GetProgress(string videoId)
        {
            return PlaybackItems.FirstOrDefault(x => x.VideoId == videoId && !x.Completed);
        }

        public void SaveProgress(PlaybackProgress progress)
        {
            if (progress == null || String.IsNullOrWhiteSpace(progress.VideoId)) return;
            var values = PlaybackItems.Where(x => x.VideoId != progress.VideoId).ToList();
            progress.LastPlayedUtc = DateTime.UtcNow;
            progress.Completed = progress.DurationSeconds > 0 &&
                progress.PositionSeconds >= progress.DurationSeconds * 0.92;
            if (!progress.Completed && progress.DurationSeconds > 0) values.Insert(0, progress);
            settings.Values["playbackProgress"] = JsonConvert.SerializeObject(values.Take(100));
        }

        private bool ReadBoolean(string key, bool fallback)
        {
            var value = settings.Values[key];
            return value is bool ? (bool)value : fallback;
        }

        private HashSet<string> ReadIdSet(string key)
        {
            var raw = settings.Values[key] as string ?? String.Empty;
            return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
        }

        private void ToggleId(string key, string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            var values = ReadIdSet(key);
            if (!values.Add(id)) values.Remove(id);
            settings.Values[key] = String.Join("|", values);
        }

        private IList<string> ReadStringList(string key)
        {
            try
            {
                var raw = settings.Values[key] as string;
                return String.IsNullOrWhiteSpace(raw)
                    ? new List<string>()
                    : JsonConvert.DeserializeObject<List<string>>(raw) ?? new List<string>();
            }
            catch
            {
                settings.Values.Remove(key);
                return new List<string>();
            }
        }

        private void WriteStringList(string key, IEnumerable<string> values)
        {
            settings.Values[key] = JsonConvert.SerializeObject(values ?? Enumerable.Empty<string>());
        }
    }
}
