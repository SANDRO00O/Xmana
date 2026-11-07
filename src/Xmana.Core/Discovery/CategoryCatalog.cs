using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xmana.Core.Api;
using Xmana.Core.Http;
using Xmana.Core.Logging;
using Xmana.Core.Models;

namespace Xmana.Core.Discovery
{
    /// <summary>A browsable category with the languages that have content in it (from the actual API metadata).</summary>
    public sealed class CategoryEntry
    {
        public CategoryEntry(string id, string? enTitle, string? arTitle, bool isMain, IReadOnlyList<CategoryLanguage> languages)
        {
            Id = id;
            EnTitle = enTitle;
            ArTitle = arTitle;
            IsMain = isMain;
            Languages = languages;
        }

        public string Id { get; }

        public string? EnTitle { get; }

        public string? ArTitle { get; }

        public bool IsMain { get; }

        public IReadOnlyList<CategoryLanguage> Languages { get; }

        public string GetTitle(bool preferArabic)
        {
            var preferred = preferArabic ? ArTitle : EnTitle;
            return !string.IsNullOrWhiteSpace(preferred) ? preferred! : ((preferArabic ? EnTitle : ArTitle) ?? Id);
        }

        /// <summary>
        /// Languages that actually have titles of the given kind in this category (count greater than zero; an unknown count is
        /// kept), most populated first. Empty when the metadata lists none, in which case no language filter is offered.
        /// </summary>
        public IReadOnlyList<CategoryLanguage> LanguagesFor(ListingKind kind) =>
            Languages
                .Where(l => (kind == ListingKind.Movies ? l.MovieCount : l.SeriesCount) != 0)
                .OrderByDescending(l => (kind == ListingKind.Movies ? l.MovieCount : l.SeriesCount) ?? 0)
                .ThenBy(l => l.EnTitle ?? l.ArTitle ?? l.LanguageId, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public sealed class CategoryCatalog
    {
        public static readonly CategoryCatalog Empty = new CategoryCatalog(new CategoryEntry[0], false);

        public CategoryCatalog(IReadOnlyList<CategoryEntry> entries, bool hasLanguageMetadata)
        {
            All = entries;
            HasLanguageMetadata = hasLanguageMetadata;
        }

        /// <summary>Main categories first, then sub categories; each group ordered by title.</summary>
        public IReadOnlyList<CategoryEntry> All { get; }

        public bool HasLanguageMetadata { get; }

        public CategoryEntry? Find(string? id) => id == null ? null : All.FirstOrDefault(c => c.Id == id);
    }

    /// <summary>
    /// Loads <c>mainCategories</c>, <c>subCategories</c> and <c>categoryLangFreqVideo</c> and joins them by category id.
    /// A failing source degrades gracefully: without language metadata no language filter is offered; the loader only
    /// throws when no category list could be loaded at all.
    /// </summary>
    public sealed class CategoryCatalogLoader
    {
        private readonly IDiscoveryDataSource _source;
        private readonly IXmanaLogger _logger;

        public CategoryCatalogLoader(IDiscoveryDataSource source, IXmanaLogger? logger = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task<CategoryCatalog> LoadAsync(string lang, bool preferArabicOrder, CancellationToken cancellationToken)
        {
            var main = Attempt(() => _source.GetMainCategoriesAsync(lang, RequestPriority.Primary, cancellationToken), "mainCategories");
            var sub = Attempt(() => _source.GetSubCategoriesAsync(lang, RequestPriority.Secondary, cancellationToken), "subCategories");
            var languages = Attempt(() => _source.GetCategoryLanguageGroupsAsync(RequestPriority.Secondary, cancellationToken), "categoryLangFreqVideo");
            await Task.WhenAll(main, sub, languages).ConfigureAwait(false);

            var mainList = main.Result;
            var subList = sub.Result;
            if (mainList == null && subList == null)
            {
                throw new ApiException(ApiErrorKind.Network, "Categories could not be loaded.", null, "categories");
            }

            var languageById = new Dictionary<string, IReadOnlyList<CategoryLanguage>>(StringComparer.Ordinal);
            if (languages.Result != null)
            {
                foreach (var group in languages.Result.Value)
                {
                    languageById[group.CategoryId] = group.Languages;
                }
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var entries = new List<CategoryEntry>();
            Add(entries, seen, mainList?.Value, true, languageById, preferArabicOrder);
            Add(entries, seen, subList?.Value, false, languageById, preferArabicOrder);
            return new CategoryCatalog(entries, languages.Result != null && languageById.Count > 0);
        }

        private static void Add(
            List<CategoryEntry> entries,
            HashSet<string> seen,
            IReadOnlyList<CategoryInfo>? source,
            bool isMain,
            Dictionary<string, IReadOnlyList<CategoryLanguage>> languageById,
            bool preferArabic)
        {
            if (source == null)
            {
                return;
            }

            var group = new List<CategoryEntry>();
            foreach (var info in source)
            {
                if (info.Id == null || !seen.Add(info.Id))
                {
                    continue;
                }

                IReadOnlyList<CategoryLanguage>? known;
                languageById.TryGetValue(info.Id, out known);
                group.Add(new CategoryEntry(info.Id, info.EnTitle, info.ArTitle, isMain, known ?? new CategoryLanguage[0]));
            }

            entries.AddRange(group.OrderBy(c => c.GetTitle(preferArabic), StringComparer.CurrentCultureIgnoreCase));
        }

        private async Task<ApiResult<T>?> Attempt<T>(Func<Task<ApiResult<T>>> call, string label)
            where T : class
        {
            try
            {
                return await call().ConfigureAwait(false);
            }
            catch (ApiException ex)
            {
                _logger.Warn(label + " failed (" + ex.Kind + ")");
                return null;
            }
        }
    }
}
