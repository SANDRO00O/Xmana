using System;
using System.Globalization;
using System.Linq;

namespace Xmana.Core.Api
{
    /// <summary>
    /// Builds relative URLs for the documented public endpoints (API.md section 16), exactly as documented.
    /// Each method keeps its own endpoint's pagination and sort semantics; there is deliberately no generic
    /// "page/offset" abstraction (PROMPT.md section 80). Relative strings double as cache keys.
    /// </summary>
    public static class CinemanaEndpoints
    {
        public static readonly Uri BaseUri = new Uri("https://cinemana.shabakaty.cc/api/android/");

        public const string RecommendationUrl = "https://recommend.shabakaty.cc/api/recommendation/recommend";
        public const string RatingSummaryUrl = "https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types";

        public static Uri Resolve(string relative) => new Uri(BaseUri, relative);

        // ---- video information -----------------------------------------------------------------

        public static string AllVideoInfo(string id) => "allVideoInfo/id/" + Seg(id);

        /// <summary>Related videos. The meaning of every <c>kind</c> value is not established in API.md; callers pass the video's own kind.</summary>
        public static string RelatedVideos(string id, string kind, int level) =>
            "relatedVideos/id/" + Seg(id) + "/kind/" + Seg(kind) + "/level/" + Num(level);

        public static string ParentalLevel(string id) => "checkVideoParentalLevel/id/" + Seg(id);

        public static string VideoComments(string id, int limit, int offset) =>
            "videoComment/id/" + Seg(id) + "/limit/" + NonNegative(limit, nameof(limit)) + "/offset/" + NonNegative(offset, nameof(offset));

        // ---- media -----------------------------------------------------------------------------

        public static string TranscodedFiles(string id) => "transcoddedFiles/id/" + Seg(id);

        public static string TranslationFiles(string id) => "translationFiles/id/" + Seg(id);

        public static string SkippingDurations(string id) => "skippingDurations/id/" + Seg(id);

        public static string VideoThumbnails(string id) => "video_thumbnails/video_id/" + Seg(id);

        // ---- series ----------------------------------------------------------------------------

        public static string SeasonNumbers(string seriesId) => "videoSeasonNumber/id/" + Seg(seriesId);

        public static string Episodes(string seriesId) => "videoSeason/id/" + Seg(seriesId);

        // ---- discovery -------------------------------------------------------------------------

        public static string Banner(int level) => "banner/level/" + Num(level);

        public static string LatestMovies(int level, int page) =>
            "latestMovies/level/" + Num(level) + "/itemsPerPage/24/page/" + NonNegative(page, nameof(page)) + "/";

        public static string LatestSeries(int level, int page) =>
            "latestSeries/level/" + Num(level) + "/itemsPerPage/24/page/" + NonNegative(page, nameof(page)) + "/";

        public static string FeaturedMovies(int level) =>
            "featuredMovies/level/" + Num(level) + "/itemsPerPage/12/page/0";

        /// <summary>API.md: <c>offset</c> behaves like a requested result COUNT here, not a skip offset.</summary>
        public static string NewlyVideos(int level, int offsetAsCount) =>
            "newlyVideosItems/level/" + Num(level) + "/offset/" + NonNegative(offsetAsCount, nameof(offsetAsCount)) + "/";

        /// <summary>API.md: <c>offset</c> behaves like a requested result COUNT here, not a skip offset.</summary>
        public static string LatestVideos(int level, int offsetAsCount) =>
            "latestVideosItems/level/" + Num(level) + "/offset/" + NonNegative(offsetAsCount, nameof(offsetAsCount)) + "/";

        public static string PopularFilms(PopularPeriod period, int level, int page)
        {
            var code = period == PopularPeriod.Month ? "M" : period == PopularPeriod.Week ? "W" : "D";
            return "popularFilms/kind/" + code + "/level/" + Num(level) + "/page/" + NonNegative(page, nameof(page));
        }

        // ---- categories ------------------------------------------------------------------------

        public static string MainCategories(string lang) => "mainCategories?lang=" + Query(lang);

        public static string SubCategories(string lang) => "subCategories?lang=" + Query(lang);

        public static string CategoryLanguageGroups() => "categoryLangFreqVideo";

        /// <summary>Pagination model: <c>offset</c> (backend-defined position/range; fixed ~30 items observed).</summary>
        public static string VideosByCategory(string categoryId, string orderBy, ListingKind kind, int offset, int level) =>
            "videosByCategory?categoryID=" + Query(categoryId)
            + "&orderby=" + ListingSort.Require(orderBy, nameof(orderBy))
            + "&videoKind=" + ((int)kind).ToString(CultureInfo.InvariantCulture)
            + "&offset=" + NonNegative(offset, nameof(offset))
            + "&level=" + Num(level);

        public static string VideosByCategoryAndLanguage(string categoryId, string languageId, ListingKind kind, string orderBy, int level, int offset) =>
            "videosByCategoryAndLanguage?category_id=" + Query(categoryId)
            + "&language_id=" + Query(languageId)
            + "&videoKind=" + ((int)kind).ToString(CultureInfo.InvariantCulture)
            + "&orderby=" + ListingSort.Require(orderBy, nameof(orderBy))
            + "&level=" + Num(level)
            + "&offset=" + NonNegative(offset, nameof(offset));

        // ---- generic listing and search --------------------------------------------------------

        /// <summary>Pagination model: <c>pageNumber</c> (zero-based) with <c>itemsPerPage</c>. Sort behaviour differs from videosByCategory.</summary>
        public static string VideoListing(int itemsPerPage, int level, ListingKind kind, string sortParam, int pageNumber) =>
            "video/V/2/itemsPerPage/" + Positive(itemsPerPage, nameof(itemsPerPage))
            + "/level/" + Num(level)
            + "/videoKind/" + ((int)kind).ToString(CultureInfo.InvariantCulture)
            + "/sortParam/" + ListingSort.Require(sortParam, nameof(sortParam))
            + "/pageNumber/" + NonNegative(pageNumber, nameof(pageNumber));

        public static string SimpleSearch(string keyword, int pageSize, int pageNumber, int level)
        {
            var encoded = SearchEncoding.EncodeKeyword(keyword);
            var size = Positive(pageSize, nameof(pageSize));
            // API.md section 7 / 21: the first itemsPerPage segment is the frontend's constant 20; only the second is the page size.
            return "video/V/2/itemsPerPage/20"
                + "/video_title_search/" + encoded
                + "/itemsPerPage/" + size
                + "/pageNumber/" + NonNegative(pageNumber, nameof(pageNumber))
                + "/level/" + Num(level);
        }

        public static string AdvancedSearch(int level, AdvancedSearchFilters filters)
        {
            var query = (filters ?? new AdvancedSearchFilters()).ToQuery();
            return "AdvancedSearch?level=" + Num(level) + string.Concat(query.Select(p => "&" + p.Key + "=" + p.Value));
        }

        // ---- groups, collections, staff, faq ---------------------------------------------------

        public static string VideoGroups(string lang, int level) => "videoGroups/lang/" + Seg(lang) + "/level/" + Num(level);

        public static string GroupPage(string groupId, int level, int page) =>
            "videoListPagination/groupID/" + Seg(groupId) + "/level/" + Num(level) + "/itemsPerPage/12/page/" + NonNegative(page, nameof(page));

        public static string CollectionIds() => "collectionsId/";

        public static string CollectionVideos(string collectionId, int level) =>
            "collectionVideos/collectionID/" + Seg(collectionId) + "/level/" + Num(level);

        public static string Staff(string staffId, string lang, int level) =>
            "staff/actorID/" + Seg(staffId) + "/lang/" + Seg(lang) + "/level/" + Num(level);

        public static string Faq() => "faqQuestion";

        // ---- helpers ---------------------------------------------------------------------------

        private static string Seg(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A value is required.");
            }

            return Uri.EscapeDataString(value.Trim());
        }

        private static string Query(string value) => Seg(value);

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string NonNegative(int value, string name)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return Num(value);
        }

        private static string Positive(int value, string name)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            return Num(value);
        }
    }
}
