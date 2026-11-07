using System;
using System.Threading;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Discovery;
using Xmana.Core.Focus;
using Xmana.Core.Http;
using Xmana.Core.Navigation;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Title search (PROMPT.md section 25). Typing is debounced by <see cref="SearchController"/>; Enter searches immediately.
    /// The text box uses the platform keyboard (on-screen keyboard on Xbox when the box is activated with A).
    /// The controller is kept per navigation entry so Back from Details shows the same query and results.
    /// </summary>
    public sealed partial class SearchPage : Page
    {
        private SearchController _search;
        private RenderScheduler _renderer;
        private bool _settingText;

        public SearchPage()
        {
            InitializeComponent();
            XmanaFocus.SetKey(QueryBox, FocusKeys.ContentPrimary);
            Results.VideoInvoked += (s, video) =>
            {
                // Opening a result is a strong signal the search was useful: remember it for next time.
                AppServices.Library.AddSearch(_search?.ExecutedQuery);
                Navigator.OpenDetails(video);
            };
            Results.LoadMoreRequested += async (s, e) => await Safe(() => _search.LoadMoreAsync(CancellationToken.None));
            Results.RetryRequested += async (s, e) => await Safe(() => _search.RetryAsync(CancellationToken.None));
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var entry = (e.Parameter as PageArgs)?.Entry;
            if (entry == null)
            {
                return;
            }

            var localization = AppServices.Localization;
            TitleText.Text = localization.Get("Nav_Search");
            QueryBox.PlaceholderText = localization.Get("Search_Placeholder");
            ClearButton.Label = localization.Get("Search_Clear");
            AdvancedButton.Label = localization.Get("Search_Advanced");
            AdvancedButton.Glyph = "\uE71C";
            IdleHint.Text = localization.Get("Search_Idle");
            RecentHeader.Text = localization.Get("Search_Recent");
            ClearRecentButton.Label = localization.Get("Search_ClearRecent");
            ClearRecentButton.Glyph = "\uE74D";
            Results.EmptyTitle = localization.Get("Search_EmptyTitle");
            Results.EmptyBody = localization.Get("Search_EmptyBody");

            _search = PageSessions.TryGet<SearchController>(entry.Id)
                ?? PageSessions.GetOrCreate(entry.Id, () => new SearchController(AppServices.Discovery, TaskDelayer.Instance, null, AppServices.Logger));
            _renderer = new RenderScheduler(Dispatcher, Render);
            _search.Changed += OnSearchChanged;

            // Restore the text of a returning visit without triggering a new search.
            _settingText = true;
            QueryBox.Text = _search.Text;
            _settingText = false;
            Render();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            if (_search != null)
            {
                _search.Changed -= OnSearchChanged;
            }
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settingText || _search == null)
            {
                return;
            }

            ClearButton.Visibility = QueryBox.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            _search.SetText(QueryBox.Text);
        }

        private async void OnQueryKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && _search != null)
            {
                e.Handled = true;
                AppServices.Library.AddSearch(QueryBox.Text);
                await Safe(() => _search.SubmitAsync(QueryBox.Text, CancellationToken.None));
            }
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            QueryBox.Text = string.Empty;
            QueryBox.Focus(FocusState.Keyboard);
        }

        private void OnAdvancedClick(object sender, RoutedEventArgs e) => Navigator.OpenAdvancedSearch();

        private void OnSearchChanged(object sender, EventArgs e) => _renderer?.Request();

        // ---- recent searches (local only) -------------------------------------------------------

        private void RenderRecent()
        {
            var recent = AppServices.Library.RecentSearches();
            RecentPanel.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            RecentList.Children.Clear();
            foreach (var term in recent)
            {
                var button = new Xmana.App.Controls.ChoiceButton { Label = term, Glyph = "\uE81C", HorizontalAlignment = HorizontalAlignment.Stretch };
                var picked = term;
                button.Click += async (s, e) =>
                {
                    QueryBox.Text = picked;
                    AppServices.Library.AddSearch(picked); // moves it to the top
                    await Safe(() => _search.SubmitAsync(picked, CancellationToken.None));
                };
                RecentList.Children.Add(button);
            }
        }

        private void OnClearRecentClick(object sender, RoutedEventArgs e)
        {
            AppServices.Library.ClearSearches();
            RenderRecent();
            QueryBox.Focus(FocusState.Keyboard);
        }

        private void Render()
        {
            var snapshot = _search.Snapshot;
            IdleHost.Visibility = snapshot.State == ResultsState.Idle ? Visibility.Visible : Visibility.Collapsed;
            if (snapshot.State == ResultsState.Idle)
            {
                RenderRecent();
            }
            ClearButton.Visibility = QueryBox.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            Results.Render(snapshot, "search");
        }

        private static async System.Threading.Tasks.Task Safe(Func<System.Threading.Tasks.Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception)
            {
                // failures are reflected in the controller state
            }
        }
    }
}
