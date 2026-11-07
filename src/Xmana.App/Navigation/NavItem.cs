using System;
using System.Collections.Generic;
using System.Linq;
using Xmana.App.Views;
using Xmana.Core.Localization;

namespace Xmana.App.Navigation
{
    /// <summary>A top-level destination in the navigation bar.</summary>
    internal sealed class NavItem
    {
        public NavItem(string id, string labelKey, Type pageType)
        {
            Id = id;
            LabelKey = labelKey;
            PageType = pageType;
        }

        public string Id { get; }
        public string LabelKey { get; }
        public Type PageType { get; }

        public static readonly NavItem Home = new NavItem("home", K.NavHome, typeof(SectionPage));

        public static readonly IReadOnlyList<NavItem> All = new[]
        {
            Home,
            new NavItem("movies", K.NavMovies, typeof(SectionPage)),
            new NavItem("series", K.NavSeries, typeof(SectionPage)),
            new NavItem("anime", K.NavAnime, typeof(SectionPage)),
            new NavItem("categories", K.NavCategories, typeof(SectionPage)),
            new NavItem("search", K.NavSearch, typeof(SectionPage)),
            new NavItem("favorites", K.NavFavorites, typeof(SectionPage)),
            new NavItem("watchlist", K.NavWatchlist, typeof(SectionPage)),
            new NavItem("history", K.NavHistory, typeof(SectionPage)),
            new NavItem("settings", K.NavSettings, typeof(SettingsPage)),
        };

        public static int IndexOf(NavItem item)
        {
            for (var i = 0; i < All.Count; i++)
            {
                if (ReferenceEquals(All[i], item)) return i;
            }
            return -1;
        }

        public static NavItem? FindById(string? id) =>
            All.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.Ordinal));
    }
}
