using System.Collections.Generic;
using Xmana.Core.Caching;
using Xmana.Core.Models;

namespace Xmana.Core.Home
{
    public enum HomeSectionKind
    {
        /// <summary>Local, resumable titles with their progress (never needs the network).</summary>
        ContinueWatching,

        FeaturedMovies,

        /// <summary>
        /// Observed live behavior (API.md section 8): delivered as the <c>videoGroups</c> group whose <c>groupsID</c> is "2";
        /// there is no dedicated endpoint. Absent when that group is absent or empty.
        /// </summary>
        FeaturedSeries,
        LatestMovies,
        LatestSeries,
        NewlyAdded,
        Popular,

        /// <summary>A server-curated group; its title comes from the API (already localized by the <c>lang</c> parameter).</summary>
        Group,
    }

    public enum HomeState
    {
        /// <summary>Nothing to show yet.</summary>
        Loading,

        /// <summary>At least one section or hero item is available.</summary>
        Content,

        /// <summary>Every request succeeded but no section had items.</summary>
        Empty,

        /// <summary>Nothing could be loaded and at least one request failed.</summary>
        Error,
    }

    /// <summary>One horizontal rail of the Home page.</summary>
    public sealed class HomeSection
    {
        public HomeSection(string id, HomeSectionKind kind, string? title, IReadOnlyList<VideoSummary> items, bool canLoadMore, string? groupId = null, IReadOnlyDictionary<string, double>? progress = null)
        {
            Progress = progress;
            Id = id;
            Kind = kind;
            Title = title;
            Items = items;
            CanLoadMore = canLoadMore;
            GroupId = groupId;
        }

        /// <summary>Stable identity used for focus restoration (e.g. "latestMovies", "group:404").</summary>
        public string Id { get; }

        public HomeSectionKind Kind { get; }

        /// <summary>Server title for groups (and Featured Series when the API supplies one); null when a localized resource is used.</summary>
        public string? Title { get; }

        public IReadOnlyList<VideoSummary> Items { get; }

        public bool CanLoadMore { get; }

        /// <summary>Progress fractions (0..1) by video id, for sections that show them (Continue Watching); null otherwise.</summary>
        public IReadOnlyDictionary<string, double>? Progress { get; }

        /// <summary>The <c>groupsID</c> for sections that come from <c>videoGroups</c> (groups and Featured Series); used for pagination.</summary>
        public string? GroupId { get; }

        internal HomeSection With(IReadOnlyList<VideoSummary> items, bool canLoadMore) => new HomeSection(Id, Kind, Title, items, canLoadMore, GroupId, Progress);
    }
}
