using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;

namespace Xmana.Services
{
    public sealed class PlaybackService : IDisposable
    {
        private MediaPlayer player;
        public MediaPlayerElement View { get; private set; }
        public event EventHandler<string> PlaybackFailed;

        public PlaybackService()
        {
            player = new MediaPlayer { AutoPlay = false };
            player.MediaFailed += OnMediaFailed;
            View = new MediaPlayerElement
            {
                AreTransportControlsEnabled = true,
                AutoPlay = false,
                Stretch = Windows.UI.Xaml.Media.Stretch.Uniform
            };
            View.SetMediaPlayer(player);
        }

        public void Play(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                (uri.Scheme != "https" && uri.Scheme != "http"))
                throw new ArgumentException("The API did not return a usable media URL.");
            player.Source = MediaSource.CreateFromUri(uri);
            player.Play();
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            // Never expose media URLs: the source can include a temporary signed URL.
            PlaybackFailed?.Invoke(this, args.ErrorMessage ?? "Playback failed.");
        }

        public void Dispose()
        {
            if (player == null) return;
            player.MediaFailed -= OnMediaFailed;
            player.Dispose();
            player = null;
        }

        public static Uri FindStreamUrl(JToken token)
        {
            var candidates = new List<string>();
            Walk(token, candidates, false);
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
