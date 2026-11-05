using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;

namespace Xmana.Services
{
    public sealed class PlaybackService : IDisposable
    {
        private MediaPlayer player;
        private TimeSpan? pendingStartPosition;

        public MediaPlayerElement View { get; private set; }
        public event EventHandler<string> PlaybackFailed;

        public PlaybackService()
        {
            player = new MediaPlayer { AutoPlay = false };
            player.MediaFailed += OnMediaFailed;
            View = new MediaPlayerElement
            {
                AreTransportControlsEnabled = false,
                AutoPlay = false,
                Stretch = Windows.UI.Xaml.Media.Stretch.Uniform
            };
            View.SetMediaPlayer(player);
        }

        public TimeSpan Position => player?.PlaybackSession.Position ?? TimeSpan.Zero;
        public TimeSpan Duration => player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero;
        public bool CanSeek => player != null && player.PlaybackSession.CanSeek;
        public bool IsPlaying => player != null &&
            player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;

        public void Play(Uri uri, TimeSpan? startPosition = null)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new ArgumentException("The API did not return a usable media URL.");

            player.MediaOpened -= OnMediaOpened;
            pendingStartPosition = startPosition;
            if (pendingStartPosition.HasValue) player.MediaOpened += OnMediaOpened;
            player.Source = MediaSource.CreateFromUri(uri);
            player.Play();
        }

        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            if (pendingStartPosition.HasValue && sender.PlaybackSession.CanSeek)
                sender.PlaybackSession.Position = pendingStartPosition.Value;
            pendingStartPosition = null;
            sender.MediaOpened -= OnMediaOpened;
        }

        public void TogglePlayPause()
        {
            if (player == null) return;
            if (IsPlaying) player.Pause();
            else player.Play();
        }

        public void Pause()
        {
            if (player != null) player.Pause();
        }

        public void Seek(TimeSpan position)
        {
            if (player == null || !CanSeek) return;
            var duration = Duration;
            var clamped = position < TimeSpan.Zero ? TimeSpan.Zero :
                (duration > TimeSpan.Zero && position > duration ? duration : position);
            player.PlaybackSession.Position = clamped;
        }

        public void SeekBy(TimeSpan amount) => Seek(Position + amount);

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            // Do not surface the platform error text because it can include source details.
            PlaybackFailed?.Invoke(this, "Playback failed. Try another available quality or return to details.");
        }

        public void Dispose()
        {
            if (player == null) return;
            player.MediaFailed -= OnMediaFailed;
            player.MediaOpened -= OnMediaOpened;
            player.Pause();
            player.Dispose();
            player = null;
        }

        public static IList<string> FindAvailableQualities(JToken token)
        {
            var qualities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectQualities(token, qualities);
            return qualities.OrderByDescending(value =>
            {
                var digits = Regex.Match(value ?? String.Empty, @"^\d+");
                int number;
                return digits.Success && Int32.TryParse(digits.Value, out number) ? number : 0;
            }).ToList();
        }

        public static Uri FindStreamUrl(JToken token, string preferredQuality = null)
        {
            if (token == null) return null;

            if (!String.IsNullOrWhiteSpace(preferredQuality) &&
                !String.Equals(preferredQuality, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var candidate in FindQualityObjects(token, preferredQuality))
                {
                    var urls = new List<string>();
                    Walk(candidate, urls, false);
                    var selected = FirstHttpUri(urls);
                    if (selected != null) return selected;
                }
            }

            var fallback = new List<string>();
            Walk(token, fallback, false);
            return FirstHttpUri(fallback);
        }

        private static IEnumerable<JObject> FindQualityObjects(JToken token, string quality)
        {
            var objects = new List<JObject>();
            CollectQualityObjects(token, quality, objects);
            return objects.OrderBy(obj => obj.Descendants().Count());
        }

        private static void CollectQualityObjects(JToken token, string quality, IList<JObject> result)
        {
            if (token == null) return;
            var obj = token as JObject;
            if (obj != null)
            {
                var hasQuality = obj.DescendantsAndSelf()
                    .OfType<JValue>()
                    .Any(value => value.Type == JTokenType.String &&
                        String.Equals(value.ToString(), quality, StringComparison.OrdinalIgnoreCase));
                if (hasQuality)
                {
                    var urls = new List<string>();
                    Walk(obj, urls, false);
                    if (FirstHttpUri(urls) != null) result.Add(obj);
                }
                foreach (var property in obj.Properties())
                    CollectQualityObjects(property.Value, quality, result);
                return;
            }

            var array = token as JArray;
            if (array != null)
                foreach (var child in array) CollectQualityObjects(child, quality, result);
        }

        private static void CollectQualities(JToken token, ISet<string> qualities)
        {
            if (token == null) return;
            var value = token as JValue;
            if (value != null && value.Type == JTokenType.String)
            {
                var label = value.ToString();
                if (Regex.IsMatch(label, @"^\d{3,4}p$", RegexOptions.IgnoreCase))
                    qualities.Add(label);
                return;
            }

            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var property in obj.Properties()) CollectQualities(property.Value, qualities);
                return;
            }

            var array = token as JArray;
            if (array != null)
                foreach (var child in array) CollectQualities(child, qualities);
        }

        private static Uri FirstHttpUri(IEnumerable<string> candidates)
        {
            foreach (var value in candidates)
            {
                Uri uri;
                if (Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                    (uri.Scheme == "https" || uri.Scheme == "http")) return uri;
            }
            return null;
        }

        private static void Walk(JToken token, IList<string> candidates, bool mediaContext)
        {
            if (token == null) return;
            var obj = token as JObject;
            if (obj != null)
            {
                foreach (var property in obj.Properties())
                {
                    var key = property.Name.ToLowerInvariant();
                    var likelyMedia = mediaContext || key.Contains("url") || key.Contains("file") ||
                        key.Contains("src") || key.Contains("link") || key.Contains("path");
                    if (property.Value.Type == JTokenType.String && likelyMedia)
                        candidates.Add(property.Value.ToString());
                    else
                        Walk(property.Value, candidates, likelyMedia);
                }
                return;
            }

            var array = token as JArray;
            if (array != null)
                foreach (var child in array) Walk(child, candidates, mediaContext);
        }
    }
}
