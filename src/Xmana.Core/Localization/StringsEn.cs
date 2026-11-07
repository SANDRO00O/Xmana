using System.Collections.Generic;

namespace Xmana.Core.Localization
{
    public static class StringsEn
    {
        public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>
        {
            [K.AppName] = "Xmana",
            [K.AppTagline] = "Xmana — Movies, Series & Anime",
            [K.AppUnofficial] = "Xmana is an unofficial client and is not affiliated with or endorsed by Cinemana, Shabakaty, Microsoft, or Netflix.",

            [K.NavHome] = "Home",
            [K.NavMovies] = "Movies",
            [K.NavSeries] = "Series",
            [K.NavAnime] = "Anime",
            [K.NavCategories] = "Categories",
            [K.NavSearch] = "Search",
            [K.NavFavorites] = "Favorites",
            [K.NavWatchlist] = "Watchlist",
            [K.NavHistory] = "History",
            [K.NavSettings] = "Settings",

            [K.HintSelect] = "Select",
            [K.HintBack] = "Back",
            [K.HintSwitchSection] = "Switch section",

            [K.SectionBuilding] = "This section is being built.",

            [K.SettingsTitle] = "Settings",
            [K.SettingsInterface] = "Interface",
            [K.SettingsLanguage] = "Language",
            [K.SettingsLanguageEnglish] = "English",
            [K.SettingsLanguageArabic] = "العربية",
            [K.SettingsAbout] = "About",
            [K.SettingsVersion] = "Version",
            [K.SettingsLegal] = "Legal and attribution",
            [K.SettingsAttribution] = "Content metadata comes from the public Cinemana/Shabakaty API. Xmana stores favorites, history and settings only on this device.",
        };
    }
}
