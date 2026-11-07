using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Xmana.Core.Api
{
    /// <summary>
    /// AdvancedSearch filters (API.md section 5). Every property is optional; only those set are sent.
    /// Notes carried over from the documentation: <see cref="StaffTitle"/> is unverified for exact lookup and
    /// <see cref="Sort"/> is unreliable on this endpoint, so the UI must not promise either.
    /// </summary>
    public sealed class AdvancedSearchFilters
    {
        public string? VideoTitle { get; set; }

        public string? StaffTitle { get; set; }

        /// <summary>Zero-based page (tested with 0 and 1, 12 items each).</summary>
        public int? Page { get; set; }

        /// <summary>Rating-related filter ("star"); documented only as behaving as a rating filter.</summary>
        public int? Star { get; set; }

        public int? YearFrom { get; set; }

        public int? YearTo { get; set; }

        /// <summary>One or more category ids; the backend treated several as OR.</summary>
        public IReadOnlyList<string> CategoryIds { get; set; } = new string[0];

        public SearchType? Type { get; set; }

        /// <summary>Passed through exactly as given; values are not validated because the endpoint's behaviour is unverified.</summary>
        public string? Sort { get; set; }

        /// <summary>A copy with another zero-based page (used for pagination without mutating the user's filters).</summary>
        public AdvancedSearchFilters WithPage(int page) => new AdvancedSearchFilters
        {
            VideoTitle = VideoTitle,
            StaffTitle = StaffTitle,
            Page = page,
            Star = Star,
            YearFrom = YearFrom,
            YearTo = YearTo,
            CategoryIds = CategoryIds,
            Type = Type,
            Sort = Sort,
        };

        /// <summary>True when at least one selection other than paging/sorting is set (an empty search is not sent).</summary>
        public bool HasCriteria =>
            !string.IsNullOrWhiteSpace(VideoTitle)
            || !string.IsNullOrWhiteSpace(StaffTitle)
            || Star.HasValue
            || YearFrom.HasValue
            || YearTo.HasValue
            || (CategoryIds != null && CategoryIds.Any(c => !string.IsNullOrWhiteSpace(c)))
            || Type.HasValue;

        /// <summary>False for a half-open or reversed year range or a negative page.</summary>
        public bool IsValid =>
            (Page ?? 0) >= 0
            && (YearFrom.HasValue == YearTo.HasValue)
            && (!YearFrom.HasValue || YearFrom.Value <= YearTo!.Value);

        /// <summary>Builds <c>name=value</c> pairs in a stable order with each value percent-encoded.</summary>
        internal IReadOnlyList<KeyValuePair<string, string>> ToQuery()
        {
            var pairs = new List<KeyValuePair<string, string>>();
            Add(pairs, "videoTitle", Clean(VideoTitle));
            Add(pairs, "staffTitle", Clean(StaffTitle));
            if (Page.HasValue)
            {
                if (Page.Value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Page));
                }

                Add(pairs, "page", Page.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (Star.HasValue)
            {
                Add(pairs, "star", Star.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (YearFrom.HasValue || YearTo.HasValue)
            {
                // API.md: year accepts a range such as 2000,2005. Both ends are required for a range.
                if (!YearFrom.HasValue || !YearTo.HasValue || YearFrom.Value > YearTo.Value)
                {
                    throw new ArgumentException("Year filter needs both ends and from <= to.");
                }

                pairs.Add(new KeyValuePair<string, string>("year", YearFrom.Value.ToString(CultureInfo.InvariantCulture) + "," + YearTo.Value.ToString(CultureInfo.InvariantCulture)));
            }

            var categories = (CategoryIds ?? new string[0]).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => Uri.EscapeDataString(c.Trim())).ToList();
            if (categories.Count > 0)
            {
                pairs.Add(new KeyValuePair<string, string>("category_id", string.Join(",", categories)));
            }

            if (Type.HasValue)
            {
                Add(pairs, "type", Type.Value == SearchType.Movies ? "movies" : "series");
            }

            Add(pairs, "sort", Clean(Sort));
            return pairs;
        }

        private static string? Clean(string? value)
        {
            var text = value?.Trim();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static void Add(List<KeyValuePair<string, string>> pairs, string name, string? value)
        {
            if (value != null)
            {
                pairs.Add(new KeyValuePair<string, string>(name, Uri.EscapeDataString(value)));
            }
        }
    }
}
