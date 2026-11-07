using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Details;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Playback;

namespace Xmana.Core.Library
{
    /// <summary>
    /// Builds the History list from local progress. Titles watched before the library existed have no local snapshot; a few of them
    /// are fetched once (cached details), remembered, and listed. A title that cannot be loaded is simply left out.
    /// </summary>
    public sealed class HistoryLoader
    {
        public const int MaxEntries = 100;
        public const int MaxHydrations = 12;

        private readonly IProgressStore _progress;
        private readonly LocalLibrary _library;
        private readonly IDetailsDataSource _source;
        private readonly IXmanaLogger _logger;

        public HistoryLoader(IProgressStore progress, LocalLibrary library, IDetailsDataSource source, IXmanaLogger? logger = null)
        {
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>The list from what is known right now (no network). Use it to render instantly, then call <see cref="LoadAsync"/>.</summary>
        public IReadOnlyList<ProgressItem> LoadLocal() =>
            WatchHistory.Build(_progress.Recent(MaxEntries), id => _library.GetSnapshot(id));

        public async Task<IReadOnlyList<ProgressItem>> LoadAsync(CancellationToken cancellationToken)
        {
            var recent = _progress.Recent(MaxEntries);
            var missing = recent.Where(p => _library.GetSnapshot(p.VideoId) == null).Take(MaxHydrations).ToList();
            foreach (var entry in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var details = (await _source.GetVideoInfoAsync(entry.VideoId, RequestPriority.Low, cancellationToken).ConfigureAwait(false)).Value;
                    if (details != null)
                    {
                        _library.RememberSnapshot(details.Summary);
                    }
                }
                catch (ApiException ex)
                {
                    _logger.Warn("history: title could not be loaded (" + ex.Kind + ")");
                }
            }

            return WatchHistory.Build(recent, id => _library.GetSnapshot(id));
        }

        /// <summary>Removes one title from History (its progress, so it also stops being offered for Continue Watching).</summary>
        public void Remove(string videoId) => _progress.Remove(videoId);
    }
}
