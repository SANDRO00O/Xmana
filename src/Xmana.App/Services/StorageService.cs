using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Caching;
using Xmana.Core.Library;
using Xmana.Core.Playback;

namespace Xmana.App.Services
{
    /// <summary>
    /// Sizes and clearing of everything stored on the device (PROMPT.md section 53 Settings / Storage). Nothing here touches the
    /// network. "Reset Local Data" removes personal data and caches but keeps the chosen interface language, so the screen stays readable.
    /// </summary>
    internal sealed class StorageService
    {
        private readonly string _apiCacheDirectory;
        private readonly string _imageCacheDirectory;
        private readonly IResponseCache _responses;
        private readonly Xmana.Core.Images.ImageDiskCache _images;
        private readonly ImageLoader _imageLoader;
        private readonly IProgressStore _progress;
        private readonly LocalLibrary _library;
        private readonly PlayerPreferences _preferences;
        private readonly MotionSettings _motion;

        public StorageService(
            string apiCacheDirectory,
            string imageCacheDirectory,
            IResponseCache responses,
            Xmana.Core.Images.ImageDiskCache images,
            ImageLoader imageLoader,
            IProgressStore progress,
            LocalLibrary library,
            PlayerPreferences preferences,
            MotionSettings motion)
        {
            _apiCacheDirectory = apiCacheDirectory;
            _imageCacheDirectory = imageCacheDirectory;
            _responses = responses;
            _images = images;
            _imageLoader = imageLoader;
            _progress = progress;
            _library = library;
            _preferences = preferences;
            _motion = motion;
        }

        public Task<long> ImageCacheBytesAsync() => Task.Run(() => DirectorySize.Of(_imageCacheDirectory));

        public Task<long> MetadataCacheBytesAsync() => Task.Run(() => DirectorySize.Of(_apiCacheDirectory));

        public async Task ClearImageCacheAsync()
        {
            await _images.ClearAsync().ConfigureAwait(true);
            _imageLoader.ClearMemory();
        }

        public Task ClearMetadataCacheAsync() => _responses.ClearAsync(CancellationToken.None);

        public void ClearHistory() => _progress.Clear();

        public void ClearSearchHistory() => _library.ClearSearches();

        public async Task ResetAllAsync()
        {
            await ClearImageCacheAsync().ConfigureAwait(true);
            await ClearMetadataCacheAsync().ConfigureAwait(true);
            _progress.Clear();
            _library.ClearAll();
            _preferences.Reset();
            _motion.ReduceMotion = false;
        }
    }
}
