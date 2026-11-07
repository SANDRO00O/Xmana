using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Api;
using Xmana.Core.Caching;

namespace Xmana.Tests
{
    /// <summary>Route + mapping checks for the remaining endpoints, through the real API class.</summary>
    [TestClass]
    public class ApiRouteCoverageTests
    {
        private const string Base = "https://cinemana.shabakaty.cc/api/android/";

        private static (CinemanaApi Api, FakeTransport Transport) Create(string body)
        {
            var transport = FakeTransport.Always(body);
            var source = new CachedJsonSource(new MemoryResponseCache(), CachePolicy.Default, new FakeClock(), new ListLogger());
            return (new CinemanaApi(Make.Http(transport), source, new CinemanaApiOptions { Level = 0 }), transport);
        }

        private static string Url(FakeTransport transport) => transport.Requests.Last().Uri.AbsoluteUri;

        [TestMethod]
        public async Task MainCategories_RouteAndMapping()
        {
            var (api, transport) = Create("[{\"nb\":\"84\",\"en_title\":\"Action\",\"ar_title\":\"أكشن\"}]");
            var result = await api.GetMainCategoriesAsync("en");
            Assert.AreEqual(Base + "mainCategories?lang=en", Url(transport));
            Assert.AreEqual("84", result.Value.Single().Id);
            Assert.IsTrue(result.Value.Single().IsMain);
        }

        [TestMethod]
        public async Task SubCategories_RouteAndMapping()
        {
            var (api, transport) = Create("[{\"nb\":\"9\",\"en_title\":\"Sub\"}]");
            var result = await api.GetSubCategoriesAsync("ar");
            Assert.AreEqual(Base + "subCategories?lang=ar", Url(transport));
            Assert.IsFalse(result.Value.Single().IsMain);
        }

        [TestMethod]
        public async Task CategoryLanguageGroups_RouteAndMapping()
        {
            var (api, transport) = Create("[{\"nb\":\"84\",\"en_title\":\"Action\",\"langArray\":[{\"catNb\":\"84\",\"langNb\":\"22\",\"langFreq\":\"10\",\"langFreqSeries\":\"3\"}]}]");
            var result = await api.GetCategoryLanguageGroupsAsync();
            Assert.AreEqual(Base + "categoryLangFreqVideo", Url(transport));
            Assert.AreEqual(10, result.Value.Single().Languages.Single().MovieCount);
        }

        [TestMethod]
        public async Task VideosByCategoryAndLanguage_Route()
        {
            var (api, transport) = Create("{\"info\":[{\"nb\":\"1\"}]}");
            var result = await api.GetVideosByCategoryAndLanguageAsync("84", "2", ListingKind.Series, ListingSort.ViewsDesc, 30);
            Assert.AreEqual(Base + "videosByCategoryAndLanguage?category_id=84&language_id=2&videoKind=2&orderby=views_desc&level=0&offset=30", Url(transport));
            Assert.AreEqual(1, result.Value.Count);
        }

        [TestMethod]
        public async Task FeaturedMovies_Route()
        {
            var (api, transport) = Create("[{\"nb\":\"1\"},{\"nb\":\"2\"}]");
            var result = await api.GetFeaturedMoviesAsync();
            Assert.AreEqual(Base + "featuredMovies/level/0/itemsPerPage/12/page/0", Url(transport));
            Assert.AreEqual(2, result.Value.Count);
        }

        [TestMethod]
        public async Task LatestVideos_Route()
        {
            var (api, transport) = Create("[{\"nb\":\"1\"}]");
            await api.GetLatestVideosAsync(24);
            Assert.AreEqual(Base + "latestVideosItems/level/0/offset/24/", Url(transport));
        }

        [TestMethod]
        public async Task GroupPage_Route()
        {
            var (api, transport) = Create("[{\"nb\":\"1\"}]");
            await api.GetGroupPageAsync("404", 1);
            Assert.AreEqual(Base + "videoListPagination/groupID/404/level/0/itemsPerPage/12/page/1", Url(transport));
        }

        [TestMethod]
        public async Task Seasons_RouteAndMapping()
        {
            var (api, transport) = Create("[{\"season\":\"2\"},{\"season\":\"1\"}]");
            var result = await api.GetSeasonsAsync("1119119");
            Assert.AreEqual(Base + "videoSeasonNumber/id/1119119", Url(transport));
            CollectionAssert.AreEqual(new[] { 1, 2 }, result.Value.Select(s => s.Number).ToArray());
        }

        [TestMethod]
        public async Task Staff_RouteAndMapping()
        {
            var (api, transport) = Create("{\"nb\":\"77\",\"name\":\"Minami Takayama\",\"role\":\"Actor\"}");
            var result = await api.GetStaffAsync("77", "en");
            Assert.AreEqual(Base + "staff/actorID/77/lang/en/level/0", Url(transport));
            Assert.AreEqual("Minami Takayama", result.Value.Staff!.Name);
        }
    }
}
