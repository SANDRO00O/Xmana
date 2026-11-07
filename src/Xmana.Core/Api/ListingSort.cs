using System;
using System.Linq;

namespace Xmana.Core.Api
{
    /// <summary>
    /// Sort parameter values API.md lists for <c>videosByCategory</c>, <c>videosByCategoryAndLanguage</c> and
    /// <c>video/V/2</c>. Only these strings are ever sent. Their SEMANTICS differ per endpoint (API.md section 8
    /// caveat): this class validates, it does not promise ordering.
    /// </summary>
    public static class ListingSort
    {
        public const string Asc = "asc";
        public const string Desc = "desc";
        public const string TitleAsc = "title_asc";
        public const string TitleDesc = "title_desc";
        public const string RatingAsc = "rating_asc";
        public const string RatingDesc = "rating_desc";
        public const string ViewsAsc = "views_asc";
        public const string ViewsDesc = "views_desc";
        public const string StarsAsc = "stars_asc";
        public const string StarsDesc = "stars_desc";
        public const string RAsc = "r_asc";
        public const string RDesc = "r_desc";

        public static readonly string[] All =
        {
            Asc, Desc, TitleAsc, TitleDesc, RatingAsc, RatingDesc, ViewsAsc, ViewsDesc, StarsAsc, StarsDesc, RAsc, RDesc,
        };

        public static bool IsKnown(string? value) => value != null && All.Contains(value, StringComparer.Ordinal);

        internal static string Require(string? value, string parameterName)
        {
            if (!IsKnown(value))
            {
                throw new ArgumentException("Unsupported sort value. Use one of the documented values in ListingSort.", parameterName);
            }

            return value!;
        }
    }

    /// <summary>API.md: <c>popularFilms</c> period values M, W, D.</summary>
    public enum PopularPeriod
    {
        Month,
        Week,
        Day,
    }

    /// <summary>
    /// <c>videoKind</c> as sent to listing endpoints (1 = movies, 2 = series). Intentionally separate from
    /// the AdvancedSearch <c>type</c> values ("movies"/"series").
    /// </summary>
    public enum ListingKind
    {
        Movies = 1,
        Series = 2,
    }

    /// <summary>AdvancedSearch <c>type</c> (documented working values: movies, series).</summary>
    public enum SearchType
    {
        Movies,
        Series,
    }
}
