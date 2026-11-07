using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Details;
using Xmana.Core.Http;
using Xmana.Core.Models;

namespace Xmana.Core.Playback
{
    /// <summary>
    /// Builds the <see cref="PlaybackContext"/> for a video: its titles and, for an episode, the previous/next episodes and the
    /// episode list of its season. Uses the (cached) details endpoints. Episodes always come from the root series (never from the
    /// episode id). Failing to load the series only removes the series conveniences; playback itself does not depend on them.
    /// </summary>
    public sealed class PlaybackContextLoader
    {
        private readonly IDetailsDataSource _source;

        public PlaybackContextLoader(IDetailsDataSource source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>Throws <see cref="ApiException"/> when the video's own metadata cannot be loaded; returns null when it does not exist.</summary>
        public async Task<PlaybackContext?> LoadAsync(string videoId, CancellationToken cancellationToken)
        {
            var info = await _source.GetVideoInfoAsync(videoId, RequestPriority.Hero, cancellationToken).ConfigureAwait(false);
            var details = info.Value;
            if (details == null)
            {
                return null;
            }

            var summary = details.Summary;
            var root = summary.RootSeriesId;
            var isEpisode = !string.IsNullOrWhiteSpace(root) && root!.Trim() != "0";
            if (!isEpisode)
            {
                return new PlaybackContext(details.Id, summary.EnTitle, summary.ArTitle, null, null, null, null, null, new VideoSummary[0], details.HasIntroSkipping, summary);
            }

            IReadOnlyList<VideoSummary> episodes = new VideoSummary[0];
            IReadOnlyList<SeasonInfo> seasons = new SeasonInfo[0];
            try
            {
                episodes = (await _source.GetEpisodesAsync(root!, RequestPriority.Primary, cancellationToken).ConfigureAwait(false)).Value;
            }
            catch (ApiException)
            {
                // previous/next simply stay unavailable
            }

            var context = SeriesContext.Build(root!, episodes, seasons.Select(s => s.Number), details.Id);
            return new PlaybackContext(
                details.Id,
                summary.EnTitle,
                summary.ArTitle,
                root,
                summary.Season,
                summary.EpisodeNumber,
                context.Previous,
                context.Next,
                context.SelectedEpisodes,
                details.HasIntroSkipping,
                summary);
        }
    }
}
