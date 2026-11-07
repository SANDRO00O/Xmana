using System;
using System.Collections.Generic;
using System.Linq;
using Xmana.Core.Api;

namespace Xmana.Core.Discovery
{
    public static class DiscoveryDefaults
    {
        /// <summary>API.md section 10: the generic listing was observed working with 12, 30 and 60 items per page.</summary>
        public const int ListingPageSize = 30;

        /// <summary>API.md section 7: simple search pages of 12.</summary>
        public const int SearchPageSize = 12;

        public static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(450);
    }

    /// <summary>
    /// Anime is a first-class section built from existing metadata, not a separate subsystem (PROMPT.md section 38).
    /// OBSERVED LIVE BEHAVIOR (API.md sections 9 and 17): Animation is category 57 and Japanese is language 22 in
    /// <c>categoryLangFreqVideo</c>; category 57 + language 22 returned Japanese animation movies (videoKind=1) and series
    /// (videoKind=2). These ids are live metadata, not official constants: the catalog is consulted first and these values are
    /// only the defaults; when they are missing the section falls back to the category without a language filter.
    /// </summary>
    public static class AnimeDefinition
    {
        public const string CategoryId = "57";

        public const string JapaneseLanguageId = "22";
    }

    /// <summary>One entry of the sort picker, with the honest meaning API.md supports.</summary>
    public sealed class SortChoice
    {
        public SortChoice(string value, string resourceKey)
        {
            Value = value;
            ResourceKey = resourceKey;
        }

        /// <summary>The documented <c>sortParam</c>/<c>orderby</c> value; null means "send no sort".</summary>
        public string? Value { get; }

        /// <summary>Localization key of the label.</summary>
        public string ResourceKey { get; }
    }

    /// <summary>
    /// Sort values offered in the UI. API.md section 10: only <c>asc</c>/<c>desc</c> and <c>stars_asc</c>/<c>stars_desc</c> clearly
    /// change the results with a visible, understood field. The other documented values (title, rating, views, r) are
    /// accepted by the API layer but deliberately NOT offered, because their meaning was not established.
    /// </summary>
    public static class SortChoices
    {
        public static readonly SortChoice Default = new SortChoice(ListingSort.Desc, "Sort_Default");
        public static readonly SortChoice Reverse = new SortChoice(ListingSort.Asc, "Sort_Reverse");
        public static readonly SortChoice StarsHigh = new SortChoice(ListingSort.StarsDesc, "Sort_StarsHigh");
        public static readonly SortChoice StarsLow = new SortChoice(ListingSort.StarsAsc, "Sort_StarsLow");

        /// <summary>Choices for the browse screens.</summary>
        public static readonly IReadOnlyList<SortChoice> Browse = new[] { Default, Reverse, StarsHigh, StarsLow };

        /// <summary>AdvancedSearch sorting is unreliable (API.md section 7): "no sort" comes first and is the default.</summary>
        public static readonly IReadOnlyList<SortChoice> Advanced = new[] { new SortChoice(null!, "Sort_None"), StarsHigh, StarsLow, Default, Reverse };

        public static SortChoice FindBrowse(string? value) => Browse.FirstOrDefault(c => c.Value == value) ?? Default;
    }

    /// <summary>What a browse screen shows. Value-equal so a screen can tell whether a change really needs a reload.</summary>
    public sealed class BrowseQuery : IEquatable<BrowseQuery>
    {
        public BrowseQuery(ListingKind kind, string? categoryId = null, string? languageId = null, string? sort = null)
        {
            Kind = kind;
            CategoryId = string.IsNullOrWhiteSpace(categoryId) ? null : categoryId!.Trim();
            // A language only makes sense together with a category (videosByCategoryAndLanguage needs both).
            LanguageId = CategoryId == null || string.IsNullOrWhiteSpace(languageId) ? null : languageId!.Trim();
            Sort = ListingSort.IsKnown(sort) ? sort! : ListingSort.Desc;
        }

        public ListingKind Kind { get; }

        public string? CategoryId { get; }

        public string? LanguageId { get; }

        public string Sort { get; }

        public BrowseQuery WithKind(ListingKind kind) => new BrowseQuery(kind, CategoryId, LanguageId, Sort);

        public BrowseQuery WithSort(string sort) => new BrowseQuery(Kind, CategoryId, LanguageId, sort);

        public BrowseQuery WithLanguage(string? languageId) => new BrowseQuery(Kind, CategoryId, languageId, Sort);

        public bool Equals(BrowseQuery? other) =>
            other != null && Kind == other.Kind && CategoryId == other.CategoryId && LanguageId == other.LanguageId && Sort == other.Sort;

        public override bool Equals(object? obj) => Equals(obj as BrowseQuery);

        public override int GetHashCode() => ((int)Kind * 397) ^ (CategoryId?.GetHashCode() ?? 0) ^ ((LanguageId?.GetHashCode() ?? 0) * 31) ^ Sort.GetHashCode();

        public override string ToString() => Kind + "/" + (CategoryId ?? "-") + "/" + (LanguageId ?? "-") + "/" + Sort;
    }
}
