using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Xmana.Core.Models
{
    /// <summary>One entry of <c>mainCategories</c>/<c>subCategories</c>. The id field name is not documented; see CatalogMapper.</summary>
    public sealed class CategoryInfo
    {
        public CategoryInfo(string? id, string? enTitle, string? arTitle, bool isMain, JObject raw)
        {
            Id = id;
            EnTitle = enTitle;
            ArTitle = arTitle;
            IsMain = isMain;
            Raw = raw;
        }

        public string? Id { get; }

        public string? EnTitle { get; }

        public string? ArTitle { get; }

        /// <summary>True for <c>mainCategories</c>, false for <c>subCategories</c>.</summary>
        public bool IsMain { get; }

        public JObject Raw { get; }
    }

    /// <summary>
    /// One category of <c>categoryLangFreqVideo</c> (API.md section 9): <c>nb</c>, <c>en_title</c>, <c>ar_title</c> and a
    /// <c>langArray</c> of the languages that have content in that category.
    /// </summary>
    public sealed class CategoryLanguageGroup
    {
        public CategoryLanguageGroup(string categoryId, string? enTitle, string? arTitle, IReadOnlyList<CategoryLanguage> languages, JObject raw)
        {
            CategoryId = categoryId;
            EnTitle = enTitle;
            ArTitle = arTitle;
            Languages = languages;
            Raw = raw;
        }

        public string CategoryId { get; }

        public string? EnTitle { get; }

        public string? ArTitle { get; }

        public IReadOnlyList<CategoryLanguage> Languages { get; }

        public JObject Raw { get; }
    }

    /// <summary>One <c>langArray</c> entry: catNb, langNb, langFreq, langFreqSeries, lang_ar_title, lang_en_title.</summary>
    public sealed class CategoryLanguage
    {
        public CategoryLanguage(string languageId, int? movieCount, int? seriesCount, string? arTitle, string? enTitle, JObject raw)
        {
            LanguageId = languageId;
            MovieCount = movieCount;
            SeriesCount = seriesCount;
            ArTitle = arTitle;
            EnTitle = enTitle;
            Raw = raw;
        }

        /// <summary>Server <c>langNb</c>; pass it as <c>language_id</c> to <c>videosByCategoryAndLanguage</c>.</summary>
        public string LanguageId { get; }

        /// <summary>Server <c>langFreq</c>: number of movies in this category with this language.</summary>
        public int? MovieCount { get; }

        /// <summary>Server <c>langFreqSeries</c>: number of series in this category with this language.</summary>
        public int? SeriesCount { get; }

        public string? ArTitle { get; }

        public string? EnTitle { get; }

        public JObject Raw { get; }

        public string? GetTitle(bool preferArabic)
        {
            var preferred = preferArabic ? ArTitle : EnTitle;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred : (preferArabic ? EnTitle : ArTitle);
        }
    }

    /// <summary>One entry of <c>videoGroups</c> (groupsID, title, hasPagination, content).</summary>
    public sealed class VideoGroup
    {
        public VideoGroup(string groupId, string? title, bool? hasPagination, IReadOnlyList<VideoSummary> items, JObject raw)
        {
            GroupId = groupId;
            Title = title;
            HasPagination = hasPagination;
            Items = items;
            Raw = raw;
        }

        public string GroupId { get; }

        public string? Title { get; }

        public bool? HasPagination { get; }

        /// <summary>The group's first page of content as delivered inside the group object.</summary>
        public IReadOnlyList<VideoSummary> Items { get; }

        public JObject Raw { get; }
    }

    /// <summary>Staff lookup result. The response shape is only described loosely in API.md, so the raw payload is kept.</summary>
    public sealed class StaffPage
    {
        public StaffPage(StaffMember? staff, IReadOnlyList<VideoSummary> titles, JToken raw)
        {
            Staff = staff;
            Titles = titles;
            Raw = raw;
        }

        public StaffMember? Staff { get; }

        public IReadOnlyList<VideoSummary> Titles { get; }

        public JToken Raw { get; }
    }

    /// <summary>FAQ entry. Field names are not documented, so only the raw object is exposed.</summary>
    public sealed class FaqEntry
    {
        public FaqEntry(JObject raw)
        {
            Raw = raw;
        }

        public JObject Raw { get; }
    }

    /// <summary>Server-provided season marker from <c>videoSeasonNumber</c>.</summary>
    public sealed class SeasonInfo
    {
        public SeasonInfo(int number, JToken raw)
        {
            Number = number;
            Raw = raw;
        }

        public int Number { get; }

        public JToken Raw { get; }
    }

    /// <summary>One season with its episodes in server-declared order of <c>episodeNummer</c>.</summary>
    public sealed class SeasonGroup
    {
        public SeasonGroup(int? season, IReadOnlyList<VideoSummary> episodes)
        {
            Season = season;
            Episodes = episodes;
        }

        /// <summary>Null groups episodes the server sent without a usable season value.</summary>
        public int? Season { get; }

        public IReadOnlyList<VideoSummary> Episodes { get; }
    }
}
