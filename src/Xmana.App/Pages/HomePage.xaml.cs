using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Controls;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Focus;
using Xmana.Core.Home;
using Xmana.Core.Models;
using Xmana.Core.Navigation;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Home (PROMPT.md section 20). It renders <see cref="HomeController"/> state: skeleton while nothing is available,
    /// the hero and rails as data arrives (cached data first, then updates), an offline notice, and an empty/error state
    /// with Retry. Rails are updated in place so focus is not disturbed by background refreshes.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        private readonly HomeController _home = AppServices.Home;
        private readonly Dictionary<string, RailView> _rails = new Dictionary<string, RailView>(StringComparer.Ordinal);
        private bool _renderQueued;
        private bool _active;

        public HomePage()
        {
            InitializeComponent();
            Hero.Activated += (s, video) => OpenDetails(video);
            XmanaFocus.SetKey(RetryButton, FocusKeys.ContentPrimary);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _active = true;
            _home.Changed += OnHomeChanged;
            RetryButton.Label = AppServices.Localization.Get("Home_Retry");

            // Continue Watching may have changed while another screen (the player) was open; this is local and instant.
            _home.RefreshLocal();
            Render();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _active = false;
            _home.Changed -= OnHomeChanged;
            StopShimmer();
        }

        private void OnHomeChanged(object sender, EventArgs e)
        {
            // The controller may raise from a worker thread; coalesce bursts into one render on the UI thread.
            lock (_rails)
            {
                if (_renderQueued)
                {
                    return;
                }

                _renderQueued = true;
            }

            var ignored = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (_rails)
                {
                    _renderQueued = false;
                }

                if (_active)
                {
                    Render();
                }
            });
        }

        private void Render()
        {
            var localization = AppServices.Localization;
            var state = _home.State;

            Skeleton.Visibility = state == HomeState.Loading ? Visibility.Visible : Visibility.Collapsed;
            Scroller.Visibility = state == HomeState.Content ? Visibility.Visible : Visibility.Collapsed;
            MessagePanel.Visibility = state == HomeState.Empty || state == HomeState.Error ? Visibility.Visible : Visibility.Collapsed;

            if (state == HomeState.Loading)
            {
                StartShimmer();
            }
            else
            {
                StopShimmer();
            }

            if (state == HomeState.Error || state == HomeState.Empty)
            {
                var error = state == HomeState.Error;
                MessageTitle.Text = localization.Get(error ? "Home_ErrorTitle" : "Home_EmptyTitle");
                MessageBody.Text = localization.Get(error ? "Home_ErrorBody" : "Home_EmptyBody");
            }

            if (state != HomeState.Content)
            {
                return;
            }

            OfflineNotice.Text = localization.Get("Home_OfflineNotice");
            OfflineNotice.Visibility = _home.IsShowingOutdatedData ? Visibility.Visible : Visibility.Collapsed;

            var hero = _home.Hero;
            Hero.Visibility = hero.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            Hero.SetItems(hero);

            UpdateRails(_home.Sections);
        }

        private void UpdateRails(IReadOnlyList<HomeSection> sections)
        {
            var wanted = new HashSet<string>(sections.Select(s => s.Id), StringComparer.Ordinal);
            foreach (var stale in _rails.Keys.Where(id => !wanted.Contains(id)).ToList())
            {
                Rails.Children.Remove(_rails[stale]);
                _rails.Remove(stale);
            }

            for (var position = 0; position < sections.Count; position++)
            {
                var section = sections[position];
                RailView rail;
                if (!_rails.TryGetValue(section.Id, out rail))
                {
                    rail = new RailView { SectionId = section.Id };
                    rail.VideoInvoked += (s, video) => OpenDetails(video);
                    rail.EndReached += OnRailEndReached;
                    _rails[section.Id] = rail;
                    Rails.Children.Insert(Math.Min(position, Rails.Children.Count), rail);
                }
                else
                {
                    var current = Rails.Children.IndexOf(rail);
                    if (current != position)
                    {
                        Rails.Children.RemoveAt(current);
                        Rails.Children.Insert(Math.Min(position, Rails.Children.Count), rail);
                    }
                }

                rail.SetTitle(TitleFor(section));
                rail.SetItems(section.Items);
            }
        }

        private static string TitleFor(HomeSection section)
        {
            if (section.Kind == HomeSectionKind.Group)
            {
                return section.Title ?? string.Empty;
            }

            if (section.Kind == HomeSectionKind.FeaturedSeries && !string.IsNullOrWhiteSpace(section.Title))
            {
                return section.Title; // the API title is already in the requested language
            }

            return AppServices.Localization.Get("Home_Section_" + section.Kind);
        }

        private async void OnRailEndReached(object sender, EventArgs e)
        {
            var rail = sender as RailView;
            if (rail == null)
            {
                return;
            }

            try
            {
                await _home.LoadMoreAsync(rail.SectionId, CancellationToken.None);
            }
            catch (Exception)
            {
                // pagination problems are non-fatal: the rail simply stays as it is
            }
        }

        private void OpenDetails(VideoSummary video)
        {
            // Remember what had focus so Back lands on the same card.
            AppServices.Navigation.SaveFocusKey(XmanaFocus.CurrentKey());
            AppServices.Navigation.Push(NavigationDestination.Details, video.Id);
        }

        private async void OnRetryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await _home.LoadAsync(CancellationToken.None);
            }
            catch (Exception)
            {
                // the state machine already reflects the failure
            }
        }

        private void StartShimmer()
        {
            if (!AppServices.Motion.ReduceMotion)
            {
                ShimmerStoryboard.Begin();
            }
        }

        private void StopShimmer()
        {
            ShimmerStoryboard.Stop();
            Skeleton.Opacity = 1;
        }
    }
}
