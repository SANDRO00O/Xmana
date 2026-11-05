using System;
using System.Collections.Generic;
using Windows.Storage;

namespace Xmana.Services
{
    public sealed class LocalStateStore
    {
        private readonly ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;

        public string Language
        {
            get => settings.Values["language"] as string ?? "en";
            set => settings.Values["language"] = value == "ar" ? "ar" : "en";
        }

        public HashSet<string> Favorites
        {
            get
            {
                var raw = settings.Values["favorites"] as string ?? String.Empty;
                return new HashSet<string>(raw.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);
            }
        }

        public void ToggleFavorite(string id)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            var values = Favorites;
            if (!values.Add(id)) values.Remove(id);
            settings.Values["favorites"] = String.Join("|", values);
        }

        public bool IsFavorite(string id) => Favorites.Contains(id ?? String.Empty);
    }
}
