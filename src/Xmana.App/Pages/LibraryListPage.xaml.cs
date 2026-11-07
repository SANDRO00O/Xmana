using System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Services;
using Xmana.Core.Discovery;
using Xmana.Core.Navigation;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Favorites and Watchlist (PROMPT.md section 33): the viewer's own local lists, newest first, rendered with the shared grid. They
    /// update live when a title is added or removed elsewhere. Nothing here uses an account or the network.
    /// </summary>
    public sealed partial class LibraryListPage : Page
    {
        private RenderScheduler _renderer;
        private bool _favorites;

        public LibraryListPage()
        {
            InitializeComponent();
            Results.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var entry = (e.Parameter as PageArgs)?.Entry;
            if (entry == null)
            {
                return;
            }

            _favorites = entry.Destination == NavigationDestination.Favorites;
            var localization = AppServices.Localization;
            TitleText.Text = localization.Get(_favorites ? "Nav_Favorites" : "Nav_Watchlist");
            Results.EmptyTitle = localization.Get(_favorites ? "Favorites_EmptyTitle" : "Watchlist_EmptyTitle");
            Results.EmptyBody = localization.Get(_favorites ? "Favorites_EmptyBody" : "Watchlist_EmptyBody");

            _renderer = new RenderScheduler(Dispatcher, Render);
            AppServices.Library.Changed += OnChanged;
            Render();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            AppServices.Library.Changed -= OnChanged;
        }

        private void OnChanged(object sender, EventArgs e) => _renderer?.Request();

        private void Render()
        {
            var items = _favorites ? AppServices.Library.Favorites() : AppServices.Library.Watchlist();
            var snapshot = new ResultsSnapshot(items.Count == 0 ? ResultsState.Empty : ResultsState.Content, items, false, false, false, false);
            Results.Render(snapshot, _favorites ? "favorites" : "watchlist");
        }
    }
}
