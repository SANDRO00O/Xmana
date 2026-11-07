namespace Xmana.Core.Localization
{
    /// <summary>
    /// Strongly typed localization keys. Every constant MUST exist in both
    /// <see cref="StringsEn"/> and <see cref="StringsAr"/> (checked by tools/validate_localization.py).
    /// </summary>
    public static class K
    {
        // App identity
        public const string AppName = "app.name";
        public const string AppTagline = "app.tagline";
        public const string AppUnofficial = "app.unofficial";

        // Top-level navigation
        public const string NavHome = "nav.home";
        public const string NavMovies = "nav.movies";
        public const string NavSeries = "nav.series";
        public const string NavAnime = "nav.anime";
        public const string NavCategories = "nav.categories";
        public const string NavSearch = "nav.search";
        public const string NavFavorites = "nav.favorites";
        public const string NavWatchlist = "nav.watchlist";
        public const string NavHistory = "nav.history";
        public const string NavSettings = "nav.settings";

        // Controller hints
        public const string HintSelect = "hint.select";
        public const string HintBack = "hint.back";
        public const string HintSwitchSection = "hint.switchSection";

        // Generic section state
        public const string SectionBuilding = "section.building";

        // Settings
        public const string SettingsTitle = "settings.title";
        public const string SettingsInterface = "settings.interface";
        public const string SettingsLanguage = "settings.language";
        public const string SettingsLanguageEnglish = "settings.language.en";
        public const string SettingsLanguageArabic = "settings.language.ar";
        public const string SettingsAbout = "settings.about";
        public const string SettingsVersion = "settings.version";
        public const string SettingsLegal = "settings.legal";
        public const string SettingsAttribution = "settings.attribution";
    }
}
