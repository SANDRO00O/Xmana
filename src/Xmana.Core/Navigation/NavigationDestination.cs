using System.Collections.Generic;

namespace Xmana.Core.Navigation
{
    /// <summary>Top-level destinations (PROMPT.md section 49).</summary>
    public enum NavigationDestination
    {
        Home,
        Movies,
        Series,
        Anime,
        Categories,
        Search,
        Favorites,
        Watchlist,
        History,
        Settings,

        /// <summary>Pushed detail screen (not a navigation-rail item).</summary>
        Details,

        /// <summary>Pushed cast/crew screen (not a navigation-rail item).</summary>
        Staff,

        /// <summary>Full-screen player (not a navigation-rail item).</summary>
        Player,

        /// <summary>Frequently asked questions, opened from Settings (not a navigation-rail item).</summary>
        Faq,
    }

    public static class NavigationCatalog
    {
        /// <summary>Order in which top-level destinations appear in the navigation rail.</summary>
        public static IReadOnlyList<NavigationDestination> RailOrder { get; } = new[]
        {
            NavigationDestination.Home,
            NavigationDestination.Movies,
            NavigationDestination.Series,
            NavigationDestination.Anime,
            NavigationDestination.Categories,
            NavigationDestination.Search,
            NavigationDestination.Favorites,
            NavigationDestination.Watchlist,
            NavigationDestination.History,
            NavigationDestination.Settings,
        };

        /// <summary>The destination the application starts on and that the B button returns to.</summary>
        public static NavigationDestination Root => NavigationDestination.Home;
    }
}
