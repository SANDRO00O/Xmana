using System;
using System.Collections.Generic;
using System.Linq;
using Xmana.Core.Api.Mapping;
using Xmana.Core.Models;

namespace Xmana.Core.Details
{
    /// <summary>
    /// Seasons and episodes of a series as the server describes them (PROMPT.md section 17). Episodes are grouped by the
    /// server's <c>season</c> and ordered by <c>episodeNummer</c>; nothing is ever derived from guessed ids.
    /// </summary>
    public sealed class SeriesContext
    {
        private static readonly IReadOnlyList<VideoSummary> NoEpisodes = new VideoSummary[0];

        private SeriesContext(
            string seriesId,
            IReadOnlyList<SeasonGroup> groups,
            int? selectedSeason,
            bool hasSelection,
            IReadOnlyList<VideoSummary> flatOrder,
            string? currentEpisodeId)
        {
            SeriesId = seriesId;
            Groups = groups;
            SelectedSeason = selectedSeason;
            HasSelection = hasSelection;
            CurrentEpisodeId = currentEpisodeId;

            var selected = groups.FirstOrDefault(g => g.Season == selectedSeason);
            SelectedEpisodes = hasSelection && selected != null ? selected.Episodes : NoEpisodes;

            var index = currentEpisodeId == null ? -1 : flatOrder.ToList().FindIndex(e => e.Id == currentEpisodeId);
            Previous = index > 0 ? flatOrder[index - 1] : null;
            Next = index >= 0 && index < flatOrder.Count - 1 ? flatOrder[index + 1] : null;
            TotalEpisodes = flatOrder.Count;
        }

        public string SeriesId { get; }

        /// <summary>Season groups in display order: numbered seasons ascending, then episodes without a season.</summary>
        public IReadOnlyList<SeasonGroup> Groups { get; }

        /// <summary>The selected season; null with <see cref="HasSelection"/> true means the group of episodes without a season value.</summary>
        public int? SelectedSeason { get; }

        public bool HasSelection { get; }

        public IReadOnlyList<VideoSummary> SelectedEpisodes { get; }

        /// <summary>The episode currently being viewed, when the details screen is for an episode of this series.</summary>
        public string? CurrentEpisodeId { get; }

        public VideoSummary? Previous { get; }

        public VideoSummary? Next { get; }

        public int TotalEpisodes { get; }

        public bool HasEpisodes => TotalEpisodes > 0;

        public SeriesContext WithSeason(int? season, IReadOnlyList<VideoSummary> flatOrder) =>
            new SeriesContext(SeriesId, Groups, season, true, flatOrder, CurrentEpisodeId);

        /// <summary>
        /// Builds the context. <paramref name="seasonNumbers"/> (from <c>videoSeasonNumber</c>) can add seasons that have no
        /// episode in the list yet; they appear as empty groups. The initial selection is the season of the viewed episode,
        /// otherwise the lowest season.
        /// </summary>
        public static SeriesContext Build(string seriesId, IReadOnlyList<VideoSummary> episodes, IEnumerable<int> seasonNumbers, string? currentEpisodeId)
        {
            var groups = CatalogMapper.GroupBySeason(episodes).ToList();
            foreach (var number in seasonNumbers ?? new int[0])
            {
                if (groups.All(g => g.Season != number))
                {
                    groups.Add(new SeasonGroup(number, NoEpisodes));
                }
            }

            groups = groups
                .OrderBy(g => g.Season.HasValue ? 0 : 1)
                .ThenBy(g => g.Season ?? 0)
                .ToList();

            var flat = FlatOrder(groups);
            var current = currentEpisodeId == null ? null : flat.FirstOrDefault(e => e.Id == currentEpisodeId);
            var withEpisodes = groups.Where(g => g.Episodes.Count > 0).ToList();

            int? season;
            bool has;
            if (current != null)
            {
                season = current.Season;
                has = true;
            }
            else if (groups.Count > 0)
            {
                var first = withEpisodes.Count > 0 ? withEpisodes[0] : groups[0];
                season = first.Season;
                has = true;
            }
            else
            {
                season = null;
                has = false;
            }

            return new SeriesContext(seriesId, groups, season, has, flat, current?.Id);
        }

        public static IReadOnlyList<VideoSummary> FlatOrder(IEnumerable<SeasonGroup> groups) =>
            groups.SelectMany(g => g.Episodes).ToList();
    }
}
