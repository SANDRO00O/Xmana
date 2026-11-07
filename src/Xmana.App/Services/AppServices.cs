using System.IO;
using Windows.Storage;
using Xmana.Core.Api;
using Xmana.Core.Caching;
using Xmana.Core.Details;
using Xmana.Core.Discovery;
using Xmana.Core.Home;
using Xmana.Core.Http;
using Xmana.Core.Images;
using Xmana.Core.Localization;
using Xmana.Core.Library;
using Xmana.Core.Logging;
using Xmana.Core.Navigation;
using Xmana.Core.Playback;

namespace Xmana.App.Services
{
    /// <summary>
    /// Composition root (small, explicit, no DI container). Initialised once at launch.
    /// Milestone 2 adds the network/cache stack; nothing in the UI uses it yet.
    /// </summary>
    internal static class AppServices
    {
        private static bool _initialized;

        public static ISettingsStore Settings { get; private set; }

        public static LocalizationService Localization { get; private set; }

        public static MotionSettings Motion { get; private set; }

        public static NavigationController Navigation { get; private set; }

        public static IXmanaLogger Logger { get; private set; }

        public static IResponseCache ResponseCache { get; private set; }

        /// <summary>The disk tier of <see cref="ResponseCache"/>, exposed for startup housekeeping (purge).</summary>
        public static FileResponseCache ApiCache { get; private set; }

        /// <summary>The on-disk image cache, exposed for startup housekeeping (purge) and Settings (Clear saved images).</summary>
        public static ImageDiskCache ImageDiskCache { get; private set; }

        public static ICinemanaApi Api { get; private set; }

        public static ImageLoader Images { get; private set; }

        public static HomeController Home { get; private set; }

        public static IDiscoveryDataSource Discovery { get; private set; }

        public static CategoryCatalogProvider Categories { get; private set; }

        public static IDetailsDataSource DetailsSource { get; private set; }

        public static IPlaybackDataSource PlaybackData { get; private set; }

        public static PlaybackContextLoader PlaybackContexts { get; private set; }

        public static IProgressStore Progress { get; private set; }

        public static PlayerPreferences PlayerPrefs { get; private set; }

        public static LocalLibrary Library { get; private set; }

        public static HistoryLoader History { get; private set; }

        internal static StorageService Storage { get; private set; }

        /// <summary>The session of the player that is on screen (so app suspension can save its progress); null otherwise.</summary>
        public static PlaybackSession ActivePlayback { get; set; }

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            Settings = new LocalSettingsStore();
            Localization = new LocalizationService(Settings);
            Motion = new MotionSettings(Settings);
            Navigation = new NavigationController(NavigationCatalog.Root);
            Logger = new DebugLogger();

            // Cache lives in LocalCacheFolder: the system may reclaim it, which is exactly what a cache may tolerate.
            var cacheDirectory = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "api-cache");
            ApiCache = new FileResponseCache(cacheDirectory);
            ResponseCache = new TieredResponseCache(new MemoryResponseCache(), ApiCache);

            var http = new ApiHttpClient(new HttpClientTransport(), new HttpClientOptions(), Logger);
            var source = new CachedJsonSource(ResponseCache, CachePolicy.Default, SystemClock.Instance, Logger);
            Api = new CinemanaApi(http, source, new CinemanaApiOptions());

            var imageDirectory = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "image-cache");
            ImageDiskCache = new ImageDiskCache(imageDirectory);
            Images = new ImageLoader(new ImageDownloader(logger: Logger), ImageDiskCache, Logger);

            Discovery = new ApiDiscoveryDataSource(Api);
            Categories = new CategoryCatalogProvider(
                new CategoryCatalogLoader(Discovery, Logger),
                () => Localization.CurrentLanguage.Code,
                () => Localization.CurrentLanguage.IsRightToLeft);

            DetailsSource = new ApiDetailsDataSource(Api);
            PlaybackData = new ApiPlaybackDataSource(Api);
            PlaybackContexts = new PlaybackContextLoader(DetailsSource);
            Progress = new FileProgressStore(Path.Combine(ApplicationData.Current.LocalFolder.Path, "playback-progress.json"));
            PlayerPrefs = new PlayerPreferences(Settings);
            Library = new LocalLibrary(new FileLibraryStorage(Path.Combine(ApplicationData.Current.LocalFolder.Path, "library.json")));
            History = new HistoryLoader(Progress, Library, DetailsSource, Logger);

            // Continue Watching is purely local: resumable progress joined with remembered titles.
            Home = new HomeController(
                new ApiHomeDataSource(Api),
                () => Localization.CurrentLanguage.Code,
                TaskDelayer.Instance,
                Logger,
                () => ContinueWatching.Build(Progress.Recent(60), id => Library.GetSnapshot(id)));

            Storage = new StorageService(cacheDirectory, imageDirectory, ResponseCache, ImageDiskCache, Images, Progress, Library, PlayerPrefs, Motion);

            _initialized = true;
        }
    }
}
