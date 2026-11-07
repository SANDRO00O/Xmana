using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Xmana.App.Focus;
using Xmana.App.Services;
using Xmana.Core.Api;
using Xmana.Core.Discovery;
using Xmana.Core.Focus;
using Xmana.Core.Http;
using Xmana.Core.Models;
using Xmana.Core.Navigation;

namespace Xmana.App.Pages
{
    /// <summary>
    /// Movies, Series, Anime and category results (PROMPT.md sections 22, 23, 24, 38). The endpoint and its pagination model are
    /// chosen by <see cref="BrowseController"/>; this page only edits the query (kind, sort, language) and renders the results.
    /// The controller is kept per navigation entry, so Back from Details returns to the same list and focus.
    /// </summary>
    public sealed partial class BrowsePage : Page
    {
        private BrowseController _browse;
        private RenderScheduler _renderer;
        private NavigationEntry _entry;
        private CategoryCatalog _catalog;
        private bool _fixedKind;
        private bool _updating;
        private string _categoryId;
        private IReadOnlyList<CategoryLanguage> _languages = new CategoryLanguage[0];

        public BrowsePage()
        {
            InitializeComponent();
            Results.VideoInvoked += (s, video) => Navigator.OpenDetails(video);
            Results.LoadMoreRequested += async (s, e) => await SafeAsync(() => _browse.LoadMoreAsync(CancellationToken.None));
            Results.RetryRequested += async (s, e) => await SafeAsync(() => _browse.RetryAsync(CancellationToken.None));
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _entry = (e.Parameter as PageArgs)?.Entry;
            if (_entry == null)
            {
                return;
            }

            var localization = AppServices.Localization;
            _fixedKind = _entry.Destination == NavigationDestination.Movies || _entry.Destination == NavigationDestination.Series;
            _categoryId = _entry.Destination == NavigationDestination.Anime ? AnimeDefinition.CategoryId
                : _entry.Destination == NavigationDestination.Categories ? _entry.Parameter
                : null;

            TitleText.Text = _entry.Destination == NavigationDestination.Categories ? localization.Get("Nav_Categories") : localization.Get("Nav_" + _entry.Destination);
            MoviesButton.Label = localization.Get("Browse_KindMovies");
            SeriesButton.Label = localization.Get("Browse_KindSeries");
            XmanaFocus.SetKey(_fixedKind ? (FrameworkElement)SortBox : MoviesButton, FocusKeys.ContentPrimary);
            KindPanel.Visibility = _fixedKind ? Visibility.Collapsed : Visibility.Visible;
            SortBox.Header = localization.Get("Browse_Sort");
            LanguageBox.Header = localization.Get("Browse_Language");

            _updating = true;
            SortBox.Items.Clear();
            foreach (var choice in SortChoices.Browse)
            {
                SortBox.Items.Add(localization.Get(choice.ResourceKey));
            }

            _updating = false;

            Results.EmptyTitle = localization.Get("Browse_EmptyTitle");
            Results.EmptyBody = localization.Get("Browse_EmptyBody");

            _renderer = new RenderScheduler(Dispatcher, Render);

            var existing = PageSessions.TryGet<BrowseController>(_entry.Id);
            if (existing != null)
            {
                Attach(existing);
                await LoadCatalogAsync();
                Render();
                return;
            }

            // A fresh page: category pages need the catalog first (title, language filter, Anime's default language).
            Results.Render(new ResultsSnapshot(ResultsState.Loading, new VideoSummary[0], false, false, false, false), "browse");
            if (_categoryId != null)
            {
                await LoadCatalogAsync();
            }

            var initial = InitialQuery();
            var controller = PageSessions.GetOrCreate(_entry.Id, () => new BrowseController(AppServices.Discovery, initial, AppServices.Logger));
            Attach(controller);
            await SafeAsync(() => controller.ApplyAsync(initial, CancellationToken.None));
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _renderer?.Stop();
            if (_browse != null)
            {
                _browse.Changed -= OnBrowseChanged;
            }
        }

        private void Attach(BrowseController controller)
        {
            _browse = controller;
            _browse.Changed += OnBrowseChanged;
            SyncControls();
        }

        private BrowseQuery InitialQuery()
        {
            switch (_entry.Destination)
            {
                case NavigationDestination.Movies:
                    return new BrowseQuery(ListingKind.Movies);
                case NavigationDestination.Series:
                    return new BrowseQuery(ListingKind.Series);
                case NavigationDestination.Anime:
                {
                    // Observed live behavior: Japanese (22) is the default Anime language when the catalog offers it.
                    var kind = ListingKind.Series;
                    var language = DefaultAnimeLanguage(kind);
                    return new BrowseQuery(kind, AnimeDefinition.CategoryId, language);
                }

                default:
                    return new BrowseQuery(ListingKind.Movies, _categoryId);
            }
        }

        private string DefaultAnimeLanguage(ListingKind kind)
        {
            var entry = _catalog?.Find(AnimeDefinition.CategoryId);
            return entry != null && entry.LanguagesFor(kind).Any(l => l.LanguageId == AnimeDefinition.JapaneseLanguageId)
                ? AnimeDefinition.JapaneseLanguageId
                : null;
        }

        private async System.Threading.Tasks.Task LoadCatalogAsync()
        {
            if (_categoryId == null)
            {
                return;
            }

            try
            {
                _catalog = await AppServices.Categories.GetAsync(CancellationToken.None);
            }
            catch (ApiException)
            {
                _catalog = null; // browsing still works without language filters
            }

            var entry = _catalog?.Find(_categoryId);
            if (entry != null && _entry.Destination == NavigationDestination.Categories)
            {
                TitleText.Text = entry.GetTitle(AppServices.Localization.CurrentLanguage.IsRightToLeft);
            }

            SyncControls();
        }

        // ---- controls <-> query -----------------------------------------------------------------

        private void SyncControls()
        {
            if (_browse == null)
            {
                return;
            }

            _updating = true;
            var query = _browse.Query;
            MoviesButton.IsActive = query.Kind == ListingKind.Movies;
            SeriesButton.IsActive = query.Kind == ListingKind.Series;

            var sortIndex = SortChoices.Browse.ToList().FindIndex(c => c.Value == query.Sort);
            SortBox.SelectedIndex = Math.Max(0, sortIndex);

            FillLanguages(query);
            _updating = false;
        }

        private void FillLanguages(BrowseQuery query)
        {
            var entry = query.CategoryId == null ? null : _catalog?.Find(query.CategoryId);
            _languages = entry == null ? new CategoryLanguage[0] : entry.LanguagesFor(query.Kind);
            LanguageBox.Items.Clear();
            if (_languages.Count == 0)
            {
                LanguageBox.Visibility = Visibility.Collapsed;
                return;
            }

            var localization = AppServices.Localization;
            var preferArabic = localization.CurrentLanguage.IsRightToLeft;
            LanguageBox.Items.Add(localization.Get("Browse_AllLanguages"));
            foreach (var language in _languages)
            {
                LanguageBox.Items.Add(language.GetTitle(preferArabic) ?? language.LanguageId);
            }

            var selected = query.LanguageId == null ? -1 : _languages.ToList().FindIndex(l => l.LanguageId == query.LanguageId);
            LanguageBox.SelectedIndex = selected + 1;
            LanguageBox.Visibility = Visibility.Visible;
        }

        private async void OnKindClick(object sender, RoutedEventArgs e)
        {
            if (_browse == null)
            {
                return;
            }

            var kind = ReferenceEquals(sender, SeriesButton) ? ListingKind.Series : ListingKind.Movies;
            var query = _browse.Query.WithKind(kind);

            // Languages differ per kind: keep the selection only if it still exists for the new kind.
            var entry = query.CategoryId == null ? null : _catalog?.Find(query.CategoryId);
            if (query.LanguageId != null && (entry == null || entry.LanguagesFor(kind).All(l => l.LanguageId != query.LanguageId)))
            {
                query = query.WithLanguage(null);
            }

            await ApplyAsync(query);
        }

        private async void OnSortChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || _browse == null || SortBox.SelectedIndex < 0)
            {
                return;
            }

            await ApplyAsync(_browse.Query.WithSort(SortChoices.Browse[SortBox.SelectedIndex].Value));
        }

        private async void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || _browse == null || LanguageBox.SelectedIndex < 0)
            {
                return;
            }

            var index = LanguageBox.SelectedIndex;
            await ApplyAsync(_browse.Query.WithLanguage(index == 0 ? null : _languages[index - 1].LanguageId));
        }

        private async System.Threading.Tasks.Task ApplyAsync(BrowseQuery query)
        {
            await SafeAsync(() => _browse.ApplyAsync(query, CancellationToken.None));
            SyncControls();
        }

        // ---- rendering --------------------------------------------------------------------------

        private void OnBrowseChanged(object sender, EventArgs e) => _renderer?.Request();

        private void Render()
        {
            if (_browse != null)
            {
                Results.Render(_browse.Results.Snapshot, "browse");
            }
        }

        private static async System.Threading.Tasks.Task SafeAsync(Func<System.Threading.Tasks.Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception)
            {
                // controllers report failures through their state; nothing here may crash the UI
            }
        }
    }
}
