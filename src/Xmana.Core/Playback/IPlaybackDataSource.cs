using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>Media-related API calls. All results are temporary and never cached (they may contain signed URLs).</summary>
    public interface IPlaybackDataSource
    {
        Task<IReadOnlyList<VideoQuality>> GetQualitiesAsync(string videoId, CancellationToken cancellationToken);

        Task<TranslationInfo> GetTranslationsAsync(string videoId, CancellationToken cancellationToken);

        Task<SkipInfo> GetSkipInfoAsync(string videoId, CancellationToken cancellationToken);
    }

    public sealed class ApiPlaybackDataSource : IPlaybackDataSource
    {
        private readonly ICinemanaApi _api;

        public ApiPlaybackDataSource(ICinemanaApi api)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<IReadOnlyList<VideoQuality>> GetQualitiesAsync(string videoId, CancellationToken cancellationToken) =>
            (await _api.GetVideoFilesAsync(videoId, cancellationToken).ConfigureAwait(false)).Value;

        public async Task<TranslationInfo> GetTranslationsAsync(string videoId, CancellationToken cancellationToken) =>
            (await _api.GetTranslationsAsync(videoId, cancellationToken).ConfigureAwait(false)).Value;

        public async Task<SkipInfo> GetSkipInfoAsync(string videoId, CancellationToken cancellationToken) =>
            (await _api.GetSkipInfoAsync(videoId, cancellationToken).ConfigureAwait(false)).Value;
    }
}
