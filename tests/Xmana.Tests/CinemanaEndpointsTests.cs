using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;

namespace Xmana.Tests
{
    /// <summary>Every route is compared with the exact string documented in API.md section 16.</summary>
    [TestClass]
    public class CinemanaEndpointsTests
    {
        [TestMethod]
        public void BaseUri_IsTheDocumentedBase()
        {
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/", CinemanaEndpoints.BaseUri.AbsoluteUri);
            Assert.AreEqual("https://cinemana.shabakaty.cc/api/android/allVideoInfo/id/1119122", CinemanaEndpoints.Resolve(CinemanaEndpoints.AllVideoInfo("1119122")).AbsoluteUri);
        }

        [TestMethod]
        public void VideoRoutes_MatchDocumentation()
        {
            Assert.AreEqual("allVideoInfo/id/1119122", CinemanaEndpoints.AllVideoInfo("1119122"));
            Assert.AreEqual("relatedVideos/id/1119122/kind/1/level/0", CinemanaEndpoints.RelatedVideos("1119122", "1", 0));
            Assert.AreEqual("checkVideoParentalLevel/id/1119122", CinemanaEndpoints.ParentalLevel("1119122"));
            Assert.AreEqual("videoComment/id/1119122/limit/10/offset/0", CinemanaEndpoints.VideoComments("1119122", 10, 0));
            Assert.AreEqual("transcoddedFiles/id/1119122", CinemanaEndpoints.TranscodedFiles("1119122"));
            Assert.AreEqual("translationFiles/id/1119122", CinemanaEndpoints.TranslationFiles("1119122"));
            Assert.AreEqual("skippingDurations/id/1119122", CinemanaEndpoints.SkippingDurations("1119122"));
            Assert.AreEqual("video_thumbnails/video_id/1119122", CinemanaEndpoints.VideoThumbnails("1119122"));
            Assert.AreEqual("videoSeasonNumber/id/1119119", CinemanaEndpoints.SeasonNumbers("1119119"));
            Assert.AreEqual("videoSeason/id/1119119", CinemanaEndpoints.Episodes("1119119"));
        }

        [TestMethod]
        public void DiscoveryRoutes_MatchDocumentation_IncludingTrailingSlashes()
        {
            Assert.AreEqual("banner/level/0", CinemanaEndpoints.Banner(0));
            Assert.AreEqual("latestMovies/level/0/itemsPerPage/24/page/2/", CinemanaEndpoints.LatestMovies(0, 2));
            Assert.AreEqual("latestSeries/level/0/itemsPerPage/24/page/0/", CinemanaEndpoints.LatestSeries(0, 0));
            Assert.AreEqual("featuredMovies/level/0/itemsPerPage/12/page/0", CinemanaEndpoints.FeaturedMovies(0));
            Assert.AreEqual("newlyVideosItems/level/0/offset/12/", CinemanaEndpoints.NewlyVideos(0, 12));
            Assert.AreEqual("latestVideosItems/level/0/offset/24/", CinemanaEndpoints.LatestVideos(0, 24));
            Assert.AreEqual("popularFilms/kind/M/level/0/page/0", CinemanaEndpoints.PopularFilms(PopularPeriod.Month, 0, 0));
            Assert.AreEqual("popularFilms/kind/W/level/0/page/1", CinemanaEndpoints.PopularFilms(PopularPeriod.Week, 0, 1));
            Assert.AreEqual("popularFilms/kind/D/level/0/page/2", CinemanaEndpoints.PopularFilms(PopularPeriod.Day, 0, 2));
        }

        [TestMethod]
        public void CategoryRoutes_MatchDocumentation()
        {
            Assert.AreEqual("mainCategories?lang=en", CinemanaEndpoints.MainCategories("en"));
            Assert.AreEqual("subCategories?lang=ar", CinemanaEndpoints.SubCategories("ar"));
            Assert.AreEqual("categoryLangFreqVideo", CinemanaEndpoints.CategoryLanguageGroups());
            Assert.AreEqual(
                "videosByCategory?categoryID=84&orderby=desc&videoKind=1&offset=0&level=0",
                CinemanaEndpoints.VideosByCategory("84", ListingSort.Desc, ListingKind.Movies, 0, 0));
            Assert.AreEqual(
                "videosByCategoryAndLanguage?category_id=84&language_id=2&videoKind=2&orderby=stars_desc&level=0&offset=30",
                CinemanaEndpoints.VideosByCategoryAndLanguage("84", "2", ListingKind.Series, ListingSort.StarsDesc, 0, 30));
        }

        [TestMethod]
        public void ListingAndSearchRoutes_MatchDocumentation()
        {
            Assert.AreEqual(
                "video/V/2/itemsPerPage/12/level/0/videoKind/1/sortParam/r_desc/pageNumber/1",
                CinemanaEndpoints.VideoListing(12, 0, ListingKind.Movies, ListingSort.RDesc, 1));
            Assert.AreEqual(
                "video/V/2/itemsPerPage/20/video_title_search/Q29uYW4/itemsPerPage/12/pageNumber/0/level/0",
                CinemanaEndpoints.SimpleSearch("Conan", 12, 0, 0));
        }

        [TestMethod]
        public void GroupCollectionStaffFaqRoutes_MatchDocumentation()
        {
            Assert.AreEqual("videoGroups/lang/en/level/0", CinemanaEndpoints.VideoGroups("en", 0));
            Assert.AreEqual("videoListPagination/groupID/404/level/0/itemsPerPage/12/page/1", CinemanaEndpoints.GroupPage("404", 0, 1));
            Assert.AreEqual("collectionsId/", CinemanaEndpoints.CollectionIds());
            Assert.AreEqual("collectionVideos/collectionID/5/level/0", CinemanaEndpoints.CollectionVideos("5", 0));
            Assert.AreEqual("staff/actorID/77/lang/en/level/0", CinemanaEndpoints.Staff("77", "en", 0));
            Assert.AreEqual("faqQuestion", CinemanaEndpoints.Faq());
        }

        [TestMethod]
        public void ExternalServiceUrls_MatchDocumentation()
        {
            Assert.AreEqual("https://recommend.shabakaty.cc/api/recommendation/recommend", CinemanaEndpoints.RecommendationUrl);
            Assert.AreEqual("https://rating.shabakaty.cc/api/targets/rating-summary-by-targets-types", CinemanaEndpoints.RatingSummaryUrl);
        }

        [TestMethod]
        public void SearchKeyword_IsEncodedIntoThePath_NotRaw()
        {
            var route = CinemanaEndpoints.SimpleSearch("a b/c?d", 12, 0, 0);
            Assert.IsFalse(route.Contains(" "));
            Assert.IsFalse(route.Contains("a b"));
            Assert.IsTrue(route.Contains("/video_title_search/" + SearchEncoding.EncodeKeyword("a b/c?d") + "/itemsPerPage/"));
        }

        [TestMethod]
        public void PathValues_AreEscaped_SoTheyCannotChangeTheRoute()
        {
            Assert.AreEqual("allVideoInfo/id/1%2F..%2Fadmin", CinemanaEndpoints.AllVideoInfo("1/../admin"));
            Assert.AreEqual("mainCategories?lang=en%26x%3D1", CinemanaEndpoints.MainCategories("en&x=1"));
        }

        [TestMethod]
        public void BlankIdsAndNegativeNumbers_AreRejected()
        {
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.AllVideoInfo(""));
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.AllVideoInfo("   "));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CinemanaEndpoints.LatestMovies(0, -1));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CinemanaEndpoints.VideoListing(0, 0, ListingKind.Movies, ListingSort.Asc, 0));
        }

        [TestMethod]
        public void UnknownSortValues_AreRejected_NothingInventedIsSent()
        {
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.VideosByCategory("84", "newest", ListingKind.Movies, 0, 0));
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.VideoListing(12, 0, ListingKind.Movies, "TITLE_ASC", 0));
        }

        [TestMethod]
        public void EveryDocumentedSortValue_IsAccepted()
        {
            Assert.AreEqual(12, ListingSort.All.Length);
            foreach (var sort in ListingSort.All)
            {
                Assert.IsTrue(ListingSort.IsKnown(sort));
                CinemanaEndpoints.VideosByCategory("1", sort, ListingKind.Movies, 0, 0);
                CinemanaEndpoints.VideoListing(12, 0, ListingKind.Series, sort, 0);
            }
        }

        [TestMethod]
        public void AdvancedSearch_BuildsDocumentedFilters()
        {
            var filters = new AdvancedSearchFilters
            {
                VideoTitle = "Conan",
                Page = 1,
                Star = 7,
                YearFrom = 2000,
                YearTo = 2005,
                CategoryIds = new[] { "57", "60" },
                Type = SearchType.Series,
            };
            Assert.AreEqual(
                "AdvancedSearch?level=0&videoTitle=Conan&page=1&star=7&year=2000,2005&category_id=57,60&type=series",
                CinemanaEndpoints.AdvancedSearch(0, filters));
        }

        [TestMethod]
        public void AdvancedSearch_OnlySendsFiltersThatAreSet()
        {
            Assert.AreEqual("AdvancedSearch?level=0", CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters()));
            Assert.AreEqual("AdvancedSearch?level=0&type=movies", CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { Type = SearchType.Movies }));
            Assert.AreEqual("AdvancedSearch?level=0&videoTitle=Conan", CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { VideoTitle = "  Conan  ", StaffTitle = "  " }));
        }

        [TestMethod]
        public void AdvancedSearch_EscapesTextValues_AndPassesSortThrough()
        {
            var url = CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { VideoTitle = "Tom & Jerry=1", Sort = "stars_desc" });
            Assert.AreEqual("AdvancedSearch?level=0&videoTitle=Tom%20%26%20Jerry%3D1&sort=stars_desc", url);
        }

        [TestMethod]
        public void AdvancedSearch_InvalidYearRangeOrPage_Throws()
        {
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { YearFrom = 2005 }));
            Assert.ThrowsException<ArgumentException>(() => CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { YearFrom = 2005, YearTo = 2000 }));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => CinemanaEndpoints.AdvancedSearch(0, new AdvancedSearchFilters { Page = -1 }));
        }
    }
}
