using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xmana.Core.Focus;
using Xmana.Core.Navigation;

namespace Xmana.Tests
{
    [TestClass]
    public class NavigationControllerTests
    {
        private static List<NavigationChangedEventArgs> Record(NavigationController nav)
        {
            var events = new List<NavigationChangedEventArgs>();
            nav.Navigated += (_, e) => events.Add(e);
            return events;
        }

        [TestMethod]
        public void Starts_OnHome_WithDepthOne()
        {
            var nav = new NavigationController();
            Assert.AreEqual(NavigationDestination.Home, nav.Current.Destination);
            Assert.AreEqual(1, nav.Depth);
            Assert.IsFalse(nav.CanGoBack);
        }

        [TestMethod]
        public void Start_RaisesRootEventForInitialEntry()
        {
            var nav = new NavigationController();
            var events = Record(nav);
            nav.Start();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(NavigationKind.Root, events[0].Kind);
            Assert.IsNull(events[0].From);
            Assert.AreSame(nav.Current, events[0].To);
        }

        [TestMethod]
        public void NavigateToRoot_SameSection_IsNoOp()
        {
            var nav = new NavigationController();
            var events = Record(nav);
            Assert.IsFalse(nav.NavigateToRoot(NavigationDestination.Home));
            Assert.AreEqual(0, events.Count);
        }

        [TestMethod]
        public void NavigateToRoot_SwitchesSection_AndResetsStack()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Series, "123");
            Assert.AreEqual(2, nav.Depth);

            Assert.IsTrue(nav.NavigateToRoot(NavigationDestination.Movies));
            Assert.AreEqual(1, nav.Depth);
            Assert.AreEqual(NavigationDestination.Movies, nav.Current.Destination);
        }

        [TestMethod]
        public void NavigateToRoot_SameSectionWhilePushed_ReturnsToSectionRoot()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Series, "123");

            Assert.IsTrue(nav.NavigateToRoot(NavigationDestination.Series));
            Assert.AreEqual(1, nav.Depth);
            Assert.IsNull(nav.Current.Parameter);
        }

        [TestMethod]
        public void Back_OnHomeRoot_IsAtRoot_AndChangesNothing()
        {
            var nav = new NavigationController();
            var events = Record(nav);
            Assert.AreEqual(BackOutcome.AtRoot, nav.GoBack());
            Assert.AreEqual(0, events.Count);
            Assert.AreEqual(NavigationDestination.Home, nav.Current.Destination);
        }

        [TestMethod]
        public void Back_OnNonHomeRoot_GoesHome()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Anime);

            Assert.IsTrue(nav.CanGoBack);
            Assert.AreEqual(BackOutcome.ReturnedToHome, nav.GoBack());
            Assert.AreEqual(NavigationDestination.Home, nav.Current.Destination);
            Assert.AreEqual(BackOutcome.AtRoot, nav.GoBack());
        }

        [TestMethod]
        public void Back_OnPushedScreen_PopsToPreviousEntry()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Movies);
            var root = nav.Current;
            nav.Push(NavigationDestination.Movies, "42");
            var events = Record(nav);

            Assert.AreEqual(BackOutcome.NavigatedBack, nav.GoBack());
            Assert.AreSame(root, nav.Current);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(NavigationKind.Back, events[0].Kind);
        }

        [TestMethod]
        public void Back_IsDeterministic_FullWalkBackToHomeThenSystem()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Series, "1");
            nav.Push(NavigationDestination.Series, "2");

            Assert.AreEqual(BackOutcome.NavigatedBack, nav.GoBack());
            Assert.AreEqual(BackOutcome.NavigatedBack, nav.GoBack());
            Assert.AreEqual(NavigationDestination.Series, nav.Current.Destination);
            Assert.AreEqual(BackOutcome.ReturnedToHome, nav.GoBack());
            Assert.AreEqual(BackOutcome.AtRoot, nav.GoBack());
        }

        [TestMethod]
        public void FocusRestoration_PushedScreen_RestoresFocusOfRevealedEntry()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Movies);
            nav.SaveFocusKey("rail.featured.3");
            nav.Push(NavigationDestination.Movies, "42");

            nav.GoBack();

            Assert.AreEqual("rail.featured.3", nav.Current.SavedFocusKey);
        }

        [TestMethod]
        public void FocusRestoration_ReturningToRootSection_RestoresRememberedKey()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.SaveFocusKey("grid.item.7");
            nav.NavigateToRoot(NavigationDestination.Settings);

            nav.NavigateToRoot(NavigationDestination.Series);

            Assert.AreEqual("grid.item.7", nav.Current.SavedFocusKey);
        }

        [TestMethod]
        public void FocusRestoration_PushedEntryFocus_DoesNotLeakIntoRootMemory()
        {
            var nav = new NavigationController();
            nav.NavigateToRoot(NavigationDestination.Series);
            nav.Push(NavigationDestination.Series, "9");
            nav.SaveFocusKey("episode.4");

            nav.NavigateToRoot(NavigationDestination.Movies);
            nav.NavigateToRoot(NavigationDestination.Series);

            Assert.IsNull(nav.Current.SavedFocusKey);
        }

        [TestMethod]
        public void SaveFocusKey_IgnoresEmptyKeys()
        {
            var nav = new NavigationController();
            nav.SaveFocusKey(FocusKeys.ContentPrimary);
            nav.SaveFocusKey(null);
            nav.SaveFocusKey("");
            Assert.AreEqual(FocusKeys.ContentPrimary, nav.Current.SavedFocusKey);
        }

        [TestMethod]
        public void RailOrder_ContainsEveryDestinationExactlyOnce_HomeFirst_SettingsLast()
        {
            var order = NavigationCatalog.RailOrder;
            var all = System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Where(
                (NavigationDestination[])System.Enum.GetValues(typeof(NavigationDestination)),
                d => d != NavigationDestination.Details && d != NavigationDestination.Staff && d != NavigationDestination.Player && d != NavigationDestination.Faq));
            Assert.AreEqual(all.Length, order.Count);
            CollectionAssert.AreEquivalent(all, new List<NavigationDestination>(order));
            CollectionAssert.DoesNotContain(new List<NavigationDestination>(order), NavigationDestination.Details);
            CollectionAssert.DoesNotContain(new List<NavigationDestination>(order), NavigationDestination.Staff);
            CollectionAssert.DoesNotContain(new List<NavigationDestination>(order), NavigationDestination.Player);
            CollectionAssert.DoesNotContain(new List<NavigationDestination>(order), NavigationDestination.Faq);
            Assert.AreEqual(NavigationDestination.Home, order[0]);
            Assert.AreEqual(NavigationDestination.Settings, order[order.Count - 1]);
        }
    }
}
